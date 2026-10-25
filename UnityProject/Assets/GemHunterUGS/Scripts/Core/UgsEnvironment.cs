namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Picks the UGS environment: development for the editor and development builds, production otherwise.
    /// The server still decides what each environment allows (the fake store only works in development).
    /// </summary>
    public static class UgsEnvironment
    {
        public const string Development = "development";
        public const string Production = "production";

        public static string Resolve(string overrideName, bool isEditor, bool isDebugBuild)
        {
            if (!string.IsNullOrWhiteSpace(overrideName))
            {
                return overrideName.Trim();
            }

            return isEditor || isDebugBuild ? Development : Production;
        }
    }
}
