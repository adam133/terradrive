namespace VectorRoad.DataInversion
{
    /// <summary>
    /// Classifies a map area by its broad climate zone or biome,
    /// derived from OSM <c>country</c> or <c>addr:country</c> node tags.
    /// </summary>
    public enum RegionType
    {
        /// <summary>Region could not be determined from the available OSM data.</summary>
        Unknown,

        /// <summary>
        /// Temperate broadleaf-forest climate (four seasons, moderate rainfall).
        /// Typical of western and central Europe, most of the USA, and eastern Asia.
        /// </summary>
        Temperate,

        /// <summary>
        /// Hot desert or arid climate (very low rainfall, extreme heat).
        /// Typical of the Middle East, North Africa, and the Australian interior.
        /// </summary>
        Desert,

        /// <summary>
        /// Tropical rainforest or savanna (high heat, high humidity or seasonal rain).
        /// Typical of equatorial Africa, South-East Asia, and Central/South America.
        /// </summary>
        Tropical,

        /// <summary>
        /// Boreal (taiga) forest climate (long cold winters, short cool summers).
        /// Typical of northern Russia, Scandinavia, and northern Canada.
        /// </summary>
        Boreal,

        /// <summary>
        /// Arctic or tundra climate (permafrost, very short summers).
        /// Typical of Greenland, Iceland, and Svalbard.
        /// </summary>
        Arctic,

        /// <summary>
        /// Mediterranean climate (hot dry summers, mild wet winters).
        /// Typical of southern Europe and the Levant coast.
        /// </summary>
        Mediterranean,

        /// <summary>
        /// Steppe or semi-arid grassland (continental temperature extremes, low precipitation).
        /// Typical of Central Asia and the Eurasian steppe belt.
        /// </summary>
        Steppe,
    }
}
