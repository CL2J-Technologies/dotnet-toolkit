using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// Test de l independance des instances.
    ///
    /// Le minuteur qui declenche le depot periodique etait porte par un champ `static`. Deux
    /// instances partageaient donc le meme : la seconde creee orphelinait le minuteur de la
    /// premiere, et `Dispose` liberait celui de la derniere creee, pas le sien. **Disposer une
    /// instance arretait le depot periodique d une autre**, sans exception ni trace — le buffer
    /// continuait de se remplir et plus rien n arrivait sur le disque.
    ///
    /// Le defaut n avait pas d effet en production : une application n a qu un `LoggerProvider`,
    /// donc qu un `BufferedFileStorage`. Rien ne l imposait pour autant, et une seconde instance
    /// aurait produit une perte de journal silencieuse — la famille de pannes la plus couteuse de
    /// ce depot.
    ///
    /// ⚠️ **Ce test depend du temps**, contrairement aux autres du projet : le comportement teste
    /// est celui d un minuteur. Il attend un depot en scrutant le fichier, avec une echeance large
    /// pour ne pas devenir instable sur une machine chargee.
    /// </summary>
    public sealed class BufferedFileStorageTimerTests : IDisposable
    {
        private const int MaxSize = 1024 * 1024;
        private static readonly TimeSpan Cadence = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan Echeance = TimeSpan.FromSeconds(10);

        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public BufferedFileStorageTimerTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-timer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = root })
                .Build();

            provider = new FileStorageProviderDisk();
            provider.Initialize("Disk", configuration.GetSection("Disk"));
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Fact]
        public async Task Disposer_une_instance_n_arrete_pas_le_depot_d_une_autre()
        {
            var premiere = new BufferedFileStorage(provider, "premiere_{0:yyyyMMdd}_{1:00}.log", MaxSize, Cadence, clearFile: true);
            var seconde = new BufferedFileStorage(provider, "seconde_{0:yyyyMMdd}_{1:00}.log", MaxSize, Cadence, clearFile: true);

            // Avec le champ static, ceci liberait le minuteur de `seconde`.
            premiere.Dispose();

            await seconde.AppendAsync("ecrit par le minuteur" + Environment.NewLine);

            // Volontairement sans `FlushAsync` : c est le depot **periodique** qu on teste. Un
            // appel explicite ferait passer le test sur le code defectueux comme sur le corrige.
            var chemin = Path.Combine(root, seconde.CurrentFileName);
            var depose = await AttendreLeContenuAsync(chemin, "ecrit par le minuteur");

            seconde.Dispose();

            Assert.True(depose, $"Le minuteur de la seconde instance n a rien depose dans '{seconde.CurrentFileName}' en {Echeance.TotalSeconds} s.");
        }

        private static async Task<bool> AttendreLeContenuAsync(string chemin, string attendu)
        {
            var limite = DateTime.UtcNow.Add(Echeance);
            while (DateTime.UtcNow < limite)
            {
                if (File.Exists(chemin))
                {
                    // Lecture partagee : le fournisseur peut ecrire pendant qu on regarde.
                    using var flux = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var lecteur = new StreamReader(flux);
                    if ((await lecteur.ReadToEndAsync()).Contains(attendu, StringComparison.Ordinal))
                        return true;
                }

                await Task.Delay(50);
            }

            return false;
        }
    }
}
