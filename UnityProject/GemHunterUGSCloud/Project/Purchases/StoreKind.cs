namespace GemHunterUGSCloud.Purchases;

/// <summary>
/// Store that issued a receipt, as declared in the Unity IAP receipt envelope.
/// </summary>
public enum StoreKind
{
    Unknown,
    GooglePlay,
    AppleAppStore,
    Fake
}
