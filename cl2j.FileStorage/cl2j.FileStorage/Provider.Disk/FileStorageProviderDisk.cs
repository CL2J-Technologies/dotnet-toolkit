using cl2j.FileStorage.Core;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;

namespace cl2j.FileStorage.Provider.Disk
{
    public class FileStorageProviderDisk : IFileStorageProvider
    {
        private DirectoryInfo directory = null!;
        private const int BufferSize = 4096;

        //Suffixe des fichiers temporaires de WriteAsync. Il sert deux fois : a les nommer, et a les
        //ecarter des listings.
        private const string TemporarySuffix = ".tmp";

        public void Initialize(string providerName, IConfigurationSection configuration)
        {
            Name = providerName;

            var settings = new FileStorageProviderDiskConfiguration();
            configuration.Bind(settings);

            if (string.IsNullOrEmpty(settings.Path))
                throw new NotFoundException("FileStorageProviderDisk: Path configuration not defined.");

            if (Directory.Exists(settings.Path))
                directory = new DirectoryInfo(settings.Path);
            else
                directory = Directory.CreateDirectory(settings.Path);
        }

        public string Name { get; set; } = null!;

        public async Task<bool> ExistsAsync(string name)
        {
            var exists = File.Exists(GetName(name));
            await Task.CompletedTask;
            return exists;
        }

        public async Task<FileStoreFileInfo?> GetInfoAsync(string name)
        {
            try
            {
                await Task.CompletedTask;

                var fi = new FileInfo(GetName(name));
                if (!fi.Exists)
                    return null;

                return new FileStoreFileInfo
                {
                    Size = fi.Length,
                    CreatedOn = fi.CreationTimeUtc,
                    LastModified = fi.LastWriteTimeUtc,
                    Created = fi.CreationTime
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<IEnumerable<string>> ListFilesAsync(string path)
        {
            await Task.CompletedTask;

            var fullName = GetName(path);
            if (!Directory.Exists(fullName))
                return [];

            //Les temporaires de WriteAsync sont exclus : ils ne vivent normalement qu une fraction
            //de seconde, mais un processus tue pendant une ecriture peut en laisser un. Il ne doit
            //jamais passer pour une donnee aux yeux d un appelant qui balaie le dossier.
            var list = Directory.GetFiles(fullName).Where(n => !n.EndsWith(TemporarySuffix, StringComparison.Ordinal));
            return list.Select(n => n[(fullName.Length + 1)..]);
        }

        public async Task<IEnumerable<string>> ListFoldersAsync(string path)
        {
            await Task.CompletedTask;

            var fullName = GetName(path);
            if (!Directory.Exists(fullName))
                return [];

            var list = Directory.GetDirectories(fullName);
            return list.Select(n => n[(fullName.Length + 1)..]);
        }

        public async Task<bool> ReadAsync(string name, Stream stream)
        {
            try
            {
                //FileShare.Delete en plus, depuis que WriteAsync remplace la cible par renommage :
                //sous Windows, `MoveFileEx` echoue si le fichier remplace est ouvert sans ce
                //partage. Sans lui, une lecture en cours empecherait une ecriture qui, avant,
                //passait — la troncature, elle, ne demandait rien.
                var fs = new FileStream(GetName(name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(fs);
                await fs.CopyToAsync(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ecriture atomique : le fichier cible est soit l ancien, soit le nouveau, jamais entre
        /// les deux.
        ///
        /// **Pourquoi, depuis le 3 septembre 2026.** L implementation precedente ouvrait la cible
        /// en `FileMode.Create`, qui la **tronque a zero avant d ecrire**. Un processus interrompu
        /// entre les deux — sur Appartogo, une VM desallouee par sa Logic App pendant qu un cycle
        /// de crawl ecrivait — laissait un fichier vide ou partiel. Le stockage Azure du meme
        /// projet ne connait pas ce probleme : un blob n y devient visible qu une fois l envoi
        /// valide. Le disque local n avait pas la meme garantie ; il l a maintenant.
        ///
        /// Le temporaire est cree **dans le meme dossier** que la cible, donc sur le meme volume :
        /// c est la condition pour que `File.Move` soit atomique — `MoveFileEx` avec
        /// `MOVEFILE_REPLACE_EXISTING` sous Windows, `rename` sous Linux.
        ///
        /// `Flush(flushToDisk: true)` pousse les octets jusqu au disque avant le renommage. Sans
        /// lui, l atomicite ne tiendrait que face a la mort du processus, pas face a une coupure
        /// brutale de la machine : NTFS journalise les metadonnees, pas le contenu.
        /// </summary>
        public async Task WriteAsync(string name, Stream stream, string? contentType)
        {
            var fileName = GetName(name);
            CreateDirectory(fileName);

            var temporaryFileName = $"{fileName}.{Guid.NewGuid():N}{TemporarySuffix}";
            try
            {
                using (var outputStream = new FileStream(temporaryFileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, true))
                {
                    var bytes = new byte[stream.Length];
                    stream.Seek(0, SeekOrigin.Begin);
                    var actualCount = await stream.ReadAsync(bytes);
                    await outputStream.WriteAsync(bytes.AsMemory(0, actualCount));

                    await outputStream.FlushAsync();
                    outputStream.Flush(flushToDisk: true);
                }

                ReplaceAtomically(temporaryFileName, fileName);
            }
            catch
            {
                //Ne jamais laisser un temporaire derriere soi : il ne porte pas le nom attendu, donc
                //personne ne le lira jamais, et il grossirait le dossier a chaque echec.
                TryDelete(temporaryFileName);
                throw;
            }
        }

        //Tentatives et attente entre elles. Cinq essais espaces de 50, 100, 150 et 200 ms couvrent
        //une demi-seconde — largement au-dela de ce que dure l ouverture d un antivirus, et assez
        //court pour ne pas figer une boucle qui ecrit souvent.
        private const int TentativesDeRemplacement = 5;
        private const int AttenteEntreTentativesMs = 50;

        /// <summary>
        /// Remplace la cible par le temporaire, en un geste.
        ///
        /// `File.Replace` plutot que `File.Move(overwrite: true)` : sous Windows, Move echoue avec
        /// « Access to the path is denied » des qu un lecteur tient la cible ouverte, meme en
        /// partage. `ReplaceFile`, sur lequel Replace s appuie, est concu pour ce cas. Le detail
        /// n est pas theorique — il est verifie par un test, et l ancienne implementation par
        /// troncature n avait pas cette contrainte : la perdre aurait ete une regression.
        ///
        /// **Mais Replace a sa propre faiblesse, constatee en production le 4 septembre 2026 :**
        /// « Unable to remove the file to be replaced » — l erreur Windows 1175. `ReplaceFile` doit
        /// *supprimer* l ancienne cible, et cela echoue tant qu un tiers la tient ouverte sans
        /// autoriser la suppression. Un antivirus qui scanne le fichier qu on vient d ecrire suffit,
        /// ce qui explique que l echec soit intermittent et non systematique.
        ///
        /// Le cas s est presente sur `Results/GeocodingAddresses.json`, reecrit en entier — 11,6 Mo
        /// — a chaque adresse geocodee. Plus le fichier est gros et souvent reecrit, plus la fenetre
        /// s ouvre.
        ///
        /// On retente donc, puis on retombe sur Move en dernier recours : les deux echouent sous des
        /// conditions differentes, et celle qui bloque Replace ne bloque pas forcement Move. Si les
        /// deux echouent, l exception remonte — perdre l ecriture en silence serait pire.
        /// </summary>
        private static void ReplaceAtomically(string temporaryFileName, string fileName)
        {
            for (var tentative = 1; ; ++tentative)
            {
                try
                {
                    File.Replace(temporaryFileName, fileName, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    return;
                }
                catch (FileNotFoundException)
                {
                    //Replace exige une cible existante, Move non. C est le cas du premier ecrit.
                    File.Move(temporaryFileName, fileName, overwrite: true);
                    return;
                }
                catch (IOException) when (tentative < TentativesDeRemplacement)
                {
                    Thread.Sleep(AttenteEntreTentativesMs * tentative);
                }
                catch (IOException)
                {
                    File.Move(temporaryFileName, fileName, overwrite: true);
                    return;
                }
            }
        }

        private static void TryDelete(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
            catch
            {
                //Rien a faire de plus : on est deja dans un chemin d erreur, et masquer l exception
                //d origine par celle du menage serait pire.
            }
        }

        public async Task AppendAsync(string name, Stream stream)
        {
            var fileName = GetName(name);
            CreateDirectory(fileName);

            using var outputStream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, BufferSize, true);
            var bytes = new byte[stream.Length];
            stream.Seek(0, SeekOrigin.Begin);
            var actualCount = await stream.ReadAsync(bytes);
            await outputStream.WriteAsync(bytes.AsMemory(0, actualCount));
        }

        public async Task DeleteAsync(string name)
        {
            await Task.CompletedTask;
            File.Delete(GetName(name));
        }

        #region Private

        private static void CreateDirectory(string fileName)
        {
            var directory = Path.GetDirectoryName(fileName);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private string GetName(string key)
        {
            return Path.Combine(directory.FullName, key);
        }

        #endregion Private
    }
}