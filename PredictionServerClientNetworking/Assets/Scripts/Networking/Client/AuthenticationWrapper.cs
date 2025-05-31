using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public static class AuthenticationWrapper
{
    public static AuthState AuthState { get; private set; } = AuthState.NotAutheticated;

    public static async Task<AuthState> DoAuth(int maxRetries = 5)
    {
        if (AuthState == AuthState.Authenticated)
        {
            return AuthState;
        }

        if(AuthState == AuthState.Authenticating)
        {
            Debug.LogWarning("Already Authenticating");
            await Authenticating();
            return AuthState;
        }

        await SignInAnonymouslyAsync(maxRetries);

        return AuthState;
    }

    // check if in state of authenticating
    private static async Task<AuthState> Authenticating()
    {
        while (AuthState == AuthState.Authenticating || AuthState == AuthState.NotAutheticated)
        {
            await Task.Delay(200);
        }

        return AuthState;
    }

    private static async Task SignInAnonymouslyAsync(int maxRetries)
    {
        AuthState = AuthState.Authenticating;
        
        //Get the login method used saved in player pref
        string LoginMethod = PlayerPrefs.GetString("Login");
        int retries = 0;

        while (AuthState == AuthState.Authenticating && retries < maxRetries)
        {
            try
            {
                //Check the boolean of the following variable to decide which method to use to sign into UGS
                if (LoginMethod == "Anonymous")
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();                    
                }else if (LoginMethod == "Google")
                {
                    //Below if user uses Google to log into the game
                    //Get IDToken from Google Login to use it for signing into UGS and use its services
                    string IDToken = FirebaseClientSideAuthentication.instance.GoogleIDToken;
                    await AuthenticationService.Instance.SignInWithGoogleAsync(IDToken);
                }else if(LoginMethod == "Apple")
                {
                    //Below if user uses Apple to log into the game
                    //Get IDToken from Apple Login to use it for signing into UGS and use its services
                    string IDToken = FirebaseClientSideAuthentication.instance.AppleIDToken;
                    await AuthenticationService.Instance.SignInWithAppleAsync(IDToken);
                }else if( LoginMethod == "Email")
                {
                    string username = PlayerPrefs.GetString("Player_Name");

                    string encryptedPassword = PlayerPrefs.GetString("EmailPassword");
                    string password = DecryptString(encryptedPassword);

                    string AccountAge = PlayerPrefs.GetString("Account");
                    if (AccountAge == "New")
                    {
                        Debug.Log("[Authentication] Signing Up New User");
                        await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
                    }
                    else
                    {
                        Debug.Log("[Authentication] Signing In Existing User");
                        await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
                    }
                }         

                if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                {
                    AuthState = AuthState.Authenticated;

                    string AccountAge = PlayerPrefs.GetString("Account");
                    if(AccountAge == "New")
                    {
                        string PlayerName = PlayerPrefs.GetString("Player_Name");
                        await AuthenticationService.Instance.UpdatePlayerNameAsync(PlayerName);
                    }

                    if(await AuthenticationService.Instance.GetPlayerNameAsync() == "")
                    {
                        Debug.Log("[Authentication] Player Name is Empty");
                    }
                    else
                    {
                        FirebaseClientSidePlayerData.instance.UGSName = AuthenticationService.Instance.PlayerName;
                    }
                    //Save Data to instance for reference later

                    FirebaseClientSidePlayerData.instance.UGSID = AuthenticationService.Instance.PlayerId;
                    break;
                }
                
            }
            catch(AuthenticationException authException)// failed to autheticate
            {
                Debug.LogError(authException);
                AuthState = AuthState.Error;
            }
            catch(RequestFailedException requestException) //failed to have internet connection
            {
                Debug.LogError(requestException);
                AuthState = AuthState.Error;
            }           

            retries++;
            await Task.Delay(1000);
        }

        if(AuthState != AuthState.Authenticated)
        {
            Debug.LogWarning($"Player was not signed in sucessfully after {retries} retries");
            AuthState = AuthState.TimeOut;
        }
    }

    public static string DecryptString(string encryptedInput)
    {
        string encryptionKey = "Tod@k1q2w3e";

        if (string.IsNullOrEmpty(encryptedInput)) return string.Empty;

        try
        {
            // Convert from Base64 back to string
            byte[] bytes = System.Convert.FromBase64String(encryptedInput);
            string base64Decoded = Encoding.UTF8.GetString(bytes);

            StringBuilder decrypted = new StringBuilder();
            for (int i = 0; i < base64Decoded.Length; i++)
            {
                // XOR each character with the same key to decrypt
                char decryptedChar = (char)(base64Decoded[i] ^ encryptionKey[i % encryptionKey.Length]);
                decrypted.Append(decryptedChar);
            }

            return decrypted.ToString();
        }
        catch
        {
            Debug.LogError("Failed to decrypt string. Invalid input format.");
            return string.Empty;
        }
    }
}

public enum AuthState
{
    NotAutheticated,
    Authenticating,
    Authenticated,
    Error,
    TimeOut
}
