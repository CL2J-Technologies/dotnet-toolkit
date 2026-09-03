using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// Tests de l ecriture atomique du fournisseur disque.
    ///
    /// Ils existent a cause d un risque precis : `WriteAsync` ouvrait la cible en
    /// `FileMode.Create`, qui la tronque a zero avant d ecrire. Sur Appartogo, la machine qui
    /// agrege est desallouee tous les jours par une Logic App pendant que des cycles ecrivent
    /// toutes les trois minutes — une coupure entre la troncature et la fin de l ecriture aurait
    /// laisse un fichier vide ou partiel.
    ///
    /// L atomicite elle-meme ne se teste pas de facon deterministe : il faudrait tuer le processus
    /// a l instant precis. Ce qui se teste, et qui est ici, ce sont ses consequences observables —
    /// aucun temporaire ne survit, aucun temporaire n apparait dans un listing, un remplacement
    /// laisse exactement le nouveau contenu, et une lecture en cours ne bloque plus l ecriture.
    /// </summary>
    public sealed class FileStorageProviderDiskWriteTests : IDisposable
    {
        private readonly string racine;
        private readonly FileStorageProviderDisk provider;

        public FileStorageProviderDiskWriteTests()
        {
            racine = Path.Combine(Path.GetTempPath(), "cl2j-filestorage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(racine);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = racine })
                .Build();

            provider = new FileStorageProviderDisk();
            provider.Initialize("Disk", configuration.GetSection("Disk"));
        }

        public void Dispose()
        {
            try { Directory.Delete(racine, recursive: true); } catch { }
        }

        [Fact]
        public async Task WriteAsync_puis_ReadAsync_rend_le_meme_contenu()
        {
            var contenu = Contenu(5000);

            await EcrireAsync("fichier.bin", contenu);

            Assert.Equal(contenu, await LireAsync("fichier.bin"));
        }

        [Fact]
        public async Task WriteAsync_ne_laisse_aucun_temporaire()
        {
            await EcrireAsync("fichier.bin", Contenu(5000));

            var temporaires = Directory.GetFiles(racine, "*.tmp");
            Assert.Empty(temporaires);
        }

        [Fact]
        public async Task WriteAsync_remplace_entierement_un_fichier_plus_long()
        {
            //Le piege classique d une ecriture qui n ecraserait pas toute la cible : la queue de
            //l ancien contenu resterait collee au nouveau.
            await EcrireAsync("fichier.bin", Contenu(20000));
            var court = Contenu(300);

            await EcrireAsync("fichier.bin", court);

            Assert.Equal(court, await LireAsync("fichier.bin"));
        }

        [Fact]
        public async Task WriteAsync_reussit_pendant_qu_une_lecture_tient_le_fichier()
        {
            //Garde de non-regression : le remplacement par renommage echouerait sous Windows si le
            //fichier remplace etait ouvert sans FileShare.Delete. La troncature, elle, ne demandait
            //rien — ce test verifie qu on n a pas casse ce cas en gagnant l atomicite.
            await EcrireAsync("fichier.bin", Contenu(1000));

            var chemin = Path.Combine(racine, "fichier.bin");
            using (new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var nouveau = Contenu(2000);
                await EcrireAsync("fichier.bin", nouveau);
                Assert.Equal(nouveau, await LireAsync("fichier.bin"));
            }
        }

        [Fact]
        public async Task ListFilesAsync_ecarte_les_temporaires()
        {
            //Un processus tue pendant une ecriture peut laisser un temporaire. Il ne doit jamais
            //passer pour une donnee aux yeux d un appelant qui balaie le dossier.
            await EcrireAsync("fichier.bin", Contenu(100));
            await File.WriteAllTextAsync(Path.Combine(racine, "fichier.bin.abcdef.tmp"), "reliquat");

            var fichiers = (await provider.ListFilesAsync(string.Empty)).ToList();

            Assert.Contains("fichier.bin", fichiers);
            Assert.DoesNotContain(fichiers, f => f.EndsWith(".tmp", StringComparison.Ordinal));
        }

        [Fact]
        public async Task WriteAsync_cree_les_dossiers_manquants()
        {
            var contenu = Contenu(200);

            await EcrireAsync("un/deux/trois.bin", contenu);

            Assert.Equal(contenu, await LireAsync("un/deux/trois.bin"));
        }

        private async Task EcrireAsync(string nom, byte[] contenu)
        {
            using var source = new MemoryStream(contenu);
            await provider.WriteAsync(nom, source, null);
        }

        private async Task<byte[]> LireAsync(string nom)
        {
            using var destination = new MemoryStream();
            Assert.True(await provider.ReadAsync(nom, destination));
            return destination.ToArray();
        }

        private static byte[] Contenu(int taille)
        {
            var bytes = new byte[taille];
            for (var i = 0; i < taille; ++i)
                bytes[i] = (byte)(i % 251);
            return bytes;
        }
    }
}
