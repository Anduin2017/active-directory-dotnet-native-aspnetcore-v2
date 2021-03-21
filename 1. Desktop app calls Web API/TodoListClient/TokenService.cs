using Microsoft.Identity.Client;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace TodoListClient
{
    public class TokenService
    {
        private readonly IPublicClientApplication _app;
        private static readonly string AadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static readonly string Tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static readonly string ClientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private static readonly string ApiScope = ConfigurationManager.AppSettings["todo:TodoListScope"];
        private static readonly string Authority = string.Format(CultureInfo.InvariantCulture, AadInstance, Tenant);
        private static readonly string[] Scopes = { ApiScope };

        public TokenService()
        {
            _app = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(Authority)
                .WithDefaultRedirectUri()
                .Build();
            TokenCacheSaver.EnableSerialization(_app.UserTokenCache);
        }

        /// <summary>
        /// Summary:
        ///     Returns the current user's access token with access to Emergency Patch API.
        ///     
        /// Exceptions:
        ///   T:MsalException:
        ///     When the user failed to authenticate.
        ///     
        ///   T:UnauthorizedException
        ///     When not forcing the authentication while the user is not authenticated at all.
        /// </summary>
        /// <returns></returns>
        public async Task<AuthenticationResult> GetUser(bool force = false)
        {
            // Ensure we have authenticated accounts first.
            var authenticatedAccounts = await _app.GetAccountsAsync();
            if (!authenticatedAccounts.Any())
            {
                if (force)
                {
                    return await PromptSignIn();
                }
                throw new UnauthorizedException("Not authorzed!");
            }

            // Try to acquire a silent token.
            try
            {
                return await _app.AcquireTokenSilent(Scopes, authenticatedAccounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                if (force)
                {
                    return await PromptSignIn();
                }
                throw new UnauthorizedException("Not authorzed!");
            }
        }

        public async Task Reset()
        {
            var accounts = await _app.GetAccountsAsync();
            while (accounts.Any())
            {
                await _app.RemoveAsync(accounts.First());
                accounts = (await _app.GetAccountsAsync()).ToList();
            }
        }

        private Task<AuthenticationResult> PromptSignIn()
        {
            return _app.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();
        }
    }
}
