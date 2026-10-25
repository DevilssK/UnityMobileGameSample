using Newtonsoft.Json;

namespace GemHunterUGS.Scripts.PlayerDataManagement
{
    /// <summary>
    /// Local, cosmetic avatar choice. Profiles are no longer stored by the backend; the JSON names match the
    /// former server model so existing local saves still load.
    /// </summary>
    public class ProfilePicture
    {
        public const string PreMadeType = "pre-made";

        [JsonProperty("type")]
        public string Type { get; set; } = PreMadeType;

        [JsonProperty("imageData")]
        public string ImageData { get; set; }

        [JsonProperty("imageId")]
        public int ImageId { get; set; }
    }
}
