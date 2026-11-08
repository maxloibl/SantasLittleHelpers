using Unity.Netcode.Components;

namespace SantasLittleHelpers.Networking
{
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
