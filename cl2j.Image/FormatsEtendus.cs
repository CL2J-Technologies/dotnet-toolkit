using ImageMagick;
using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using ISImage = SixLabors.ImageSharp.Image;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace cl2j.Image
{
    /// <summary>
    /// Decodage des formats qu ImageSharp ne connait pas — HEIC en premier lieu.
    ///
    /// **Pourquoi.** Mesure du 4 septembre 2026 sur le conteneur `listing-portal` d Appartogo : sur
    /// 124 fichiers qu ImageSharp refusait, **93 sont du HEIC** et 2 de l AVIF. Ce sont des photos
    /// d iPhone, televersees telles quelles et stockees sous un nom en `.jpg`. Elles ne s affichent
    /// **nulle part** — ni Chrome ni Firefox ne rendent le HEIC — donc ces annonces n avaient aucune
    /// image visible, pleine taille comprise.
    ///
    /// Refuser ces televersements serait correct mais hostile : une part notable des visiteurs
    /// publie depuis un telephone. On les **convertit**.
    ///
    /// **Pourquoi ImageMagick et pas autre chose.** ImageSharp n a pas de decodeur HEIC et n en
    /// annonce pas. Les liaisons libheif directes exigent une bibliotheque native installee sur la
    /// machine, ce que le plan Linux du site ne permet pas de garantir. `Magick.NET-Q8-AnyCPU`
    /// embarque ses binaires natifs pour huit plateformes, `linux-x64` compris — verifie dans le
    /// paquet, pas suppose.
    ///
    /// **Il n intervient qu en repli.** ImageSharp reste le decodeur principal : il est gere, plus
    /// rapide, et couvre tout ce que le site recoit d ordinaire. ImageMagick n est appele que
    /// lorsqu ImageSharp a echoue. Ce n est pas seulement une question de vitesse — voir la note de
    /// securite ci-dessous.
    ///
    /// ⚠️ **Note de securite.** ImageMagick analyse des formats fournis par l utilisateur et a un
    /// historique de vulnerabilites suivi (« ImageTragick »). Trois precautions sont prises ici :
    /// la version est tenue a jour — **14.16.0**, la premiere sans alerte NuGet, contre onze
    /// avertissements dont quatre de severite haute en 14.9.0 ; les limites de ressources sont
    /// bornees explicitement ; et le repli n est atteint que par des octets qu ImageSharp a deja
    /// refuses, ce qui reduit la surface au lieu de l elargir. **La version doit etre suivie** :
    /// une dependance qui analyse du contenu televerse ne se laisse pas vieillir.
    /// </summary>
    internal static class FormatsEtendus
    {
        //Bornes de decodage. Sans elles, une image forgee de 60 000 x 60 000 pixels ferait allouer
        //des gigaoctets avant que quoi que ce soit ne s en apercoive. La hauteur et la largeur
        //couvrent tres largement ce qu un telephone produit — l iPhone dont vient l echantillon
        //mesure 4032 x 3024.
        private const uint LargeurMax = 20000;
        private const uint HauteurMax = 20000;
        private const ulong MemoireMax = 512 * 1024 * 1024;

        //**La mitigation qui compte le plus.** Le danger d ImageMagick ne vient pas de son decodeur
        //HEIC : il vient de son *etendue*. Il reconnait plus de deux cents formats, dont plusieurs
        //sont des langages — MSL et MVG savent lire et ecrire des fichiers — et delegue a des
        //binaires externes pour d autres. C est la famille de failles connue sous le nom
        //d ImageTragick, et elle se declenche a l *identification* du format, avant tout decodage
        //voulu : il suffit qu un fichier televerse se presente comme l un d eux.
        //
        //On ne lui laisse donc decoder que ce pour quoi il est ici. Tout le reste est refuse avant
        //qu il ne touche aux octets. La liste est courte volontairement : elle couvre les formats
        //que les appareils produisent et qu ImageSharp ne lit pas.
        private static readonly HashSet<MagickFormat> formatsAutorises =
        [
            MagickFormat.Heic,
            MagickFormat.Heif,
            MagickFormat.Avif,
        ];

        private static readonly Lock verrou = new();
        private static bool limitesPosees;

        /// <summary>
        /// Tente de decoder des octets qu ImageSharp a refuses. Rend null si ImageMagick n y arrive
        /// pas non plus, ou si le format n est pas dans la liste blanche — auquel cas ce n est pas
        /// une image qu on veuille accepter ici.
        /// </summary>
        public static ImageRgba32? Decoder(byte[] bytes)
        {
            try
            {
                PoserLesLimites();

                //Identifier d abord, decoder ensuite. MagickImageInfo lit l en-tete sans deployer
                //le decodeur complet ni les delegues : c est ce qui rend la liste blanche efficace
                //plutot que decorative.
                if (!EstAutorise(bytes))
                    return null;

                using var magick = new MagickImage(bytes);

                //Ceinture et bretelles : ce que l en-tete annonce et ce que le decodeur reconnait
                //peuvent differer sur un fichier forge.
                if (!formatsAutorises.Contains(magick.Format))
                    return null;

                //PNG et non JPEG : le repli n est qu une etape intermediaire, et l appelant
                //re-encode ensuite en JPEG. Passer par un JPEG ici ajouterait une compression avec
                //perte pour rien.
                magick.Format = MagickFormat.Png;
                var png = magick.ToByteArray();

                return ISImage.Load<Rgba32>(png);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Nomme le format sans decoder l image entiere. Sert a decider s il faut re-encoder :
        /// un HEIC doit ressortir en JPEG meme quand il ne depasse aucune dimension.
        /// </summary>
        public static string? Identifier(byte[] bytes)
        {
            try
            {
                PoserLesLimites();
                var info = new MagickImageInfo(bytes);
                return formatsAutorises.Contains(info.Format) ? info.Format.ToString() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool EstAutorise(byte[] bytes)
        {
            try
            {
                return formatsAutorises.Contains(new MagickImageInfo(bytes).Format);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void PoserLesLimites()
        {
            if (limitesPosees)
                return;

            lock (verrou)
            {
                if (limitesPosees)
                    return;

                ResourceLimits.Width = LargeurMax;
                ResourceLimits.Height = HauteurMax;
                ResourceLimits.Memory = MemoireMax;
                limitesPosees = true;
            }
        }
    }
}
