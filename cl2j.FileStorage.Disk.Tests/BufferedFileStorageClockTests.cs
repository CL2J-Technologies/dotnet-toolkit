using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// Tests de l horloge qui nomme les fichiers et decide de la bascule.
    ///
    /// Ils existent a cause d un defaut observe en production le 6 septembre 2026 : le journal
    /// horodatait ses lignes dans le fuseau configure par l application pendant que le nom du
    /// fichier et la bascule etaient en UTC en dur. Le fichier
    /// `appartogocrawler_20260906_01.log` s ouvrait sur une ligne datee du 5 a 19 h 59 — soit
    /// minuit UTC. Diagnostiquer une soiree demandait donc d ouvrir le fichier du lendemain, sans
    /// que rien ne le signale.
    ///
    /// Ce qui se teste ici, ce sont les deux consequences observables : le nom suit l horloge
    /// fournie, et la bascule aussi. Les deux comptent — n en corriger qu une les ferait diverger
    /// a nouveau, un fichier nomme pour le bon jour continuant de basculer au mauvais moment.
    /// </summary>
    public sealed class BufferedFileStorageClockTests : IDisposable
    {
        private const string Pattern = "test_{0:yyyyMMdd}_{1:00}.log";
        private const int MaxSize = 1024 * 1024;

        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public BufferedFileStorageClockTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-buffered-" + Guid.NewGuid().ToString("N"));
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

        // L instant du defaut : 20 h 30 le 5 septembre en heure de l Est, soit 00 h 30 le
        // 6 septembre en UTC. Le nom doit porter le 5, comme les lignes qu il contiendra.
        private static readonly DateTime SoireeLocale = new(2026, 9, 5, 20, 30, 0, DateTimeKind.Unspecified);

        [Fact]
        public void Le_nom_du_fichier_suit_l_horloge_fournie()
        {
            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true, () => SoireeLocale);

            Assert.Equal("test_20260905_01.log", buffered.CurrentFileName);
        }

        [Fact]
        public void Sans_horloge_le_nom_reste_en_utc()
        {
            // Le parametre est facultatif : les appelants qui ne le passent pas gardent le
            // comportement d avant le correctif.
            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true);

            Assert.Equal($"test_{DateTime.UtcNow:yyyyMMdd}_01.log", buffered.CurrentFileName);
        }

        [Fact]
        public async Task La_bascule_de_fichier_suit_l_horloge_fournie()
        {
            var maintenant = SoireeLocale;

            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true, () => maintenant);

            await buffered.AppendAsync("le soir du 5" + Environment.NewLine);
            await buffered.FlushAsync();
            Assert.Equal("test_20260905_01.log", buffered.CurrentFileName);

            // Minuit local franchi — trois heures et demie apres minuit UTC.
            maintenant = SoireeLocale.AddHours(4);

            await buffered.AppendAsync("le matin du 6" + Environment.NewLine);
            await buffered.FlushAsync();
            Assert.Equal("test_20260906_01.log", buffered.CurrentFileName);

            // Et chaque ligne est bien allee dans le fichier de sa propre journee.
            Assert.Contains("le soir du 5", await File.ReadAllTextAsync(Path.Combine(root, "test_20260905_01.log")), StringComparison.Ordinal);
            Assert.Contains("le matin du 6", await File.ReadAllTextAsync(Path.Combine(root, "test_20260906_01.log")), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Minuit_utc_ne_declenche_plus_de_bascule()
        {
            // Le coeur du defaut : a 19 h 00 puis 21 h 00 heure locale, on traverse minuit UTC
            // sans traverser minuit local. Avant le correctif, deux fichiers naissaient ici.
            var maintenant = new DateTime(2026, 9, 5, 19, 0, 0, DateTimeKind.Unspecified);

            using var buffered = new BufferedFileStorage(provider, Pattern, MaxSize, TimeSpan.FromHours(1), clearFile: true, () => maintenant);

            await buffered.AppendAsync("avant minuit UTC" + Environment.NewLine);
            await buffered.FlushAsync();

            maintenant = maintenant.AddHours(2);

            await buffered.AppendAsync("apres minuit UTC" + Environment.NewLine);
            await buffered.FlushAsync();

            Assert.Equal("test_20260905_01.log", buffered.CurrentFileName);
            Assert.Single(Directory.GetFiles(root, "test_*.log"));
        }
    }
}
