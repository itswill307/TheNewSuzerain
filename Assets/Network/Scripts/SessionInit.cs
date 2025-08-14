using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class SessionInit : MonoBehaviour
{
    public static bool Ready { get; private set; }

    async void Awake()
    {
        await EnsureReady();
    }

    public static async Task EnsureReady()
    {
        if (Ready) return;
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Ready = true;
    }
}
