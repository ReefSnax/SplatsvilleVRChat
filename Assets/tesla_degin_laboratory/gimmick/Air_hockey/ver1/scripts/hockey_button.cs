
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class hockey_button : UdonSharpBehaviour
{
    [SerializeField] private hockey_system system;

    public override void Interact()
    {
        if (!Networking.IsOwner(Networking.LocalPlayer, this.gameObject)) Networking.SetOwner(Networking.LocalPlayer, this.gameObject);

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "ButtonPress");

    }

    public void ButtonPress()
    {
        system.ButtonWhich(int.Parse(this.name));
    }
}
