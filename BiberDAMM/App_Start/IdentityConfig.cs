﻿using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BiberDAMM.DAL;
using BiberDAMM.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;

namespace BiberDAMM
{
    public class EmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            // Hier den E-Mail-Dienst einfügen, um eine E-Mail-Nachricht zu senden.
            return Task.FromResult(0);
        }
    }

    public class SmsService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            // Hier den SMS-Dienst einfügen, um eine Textnachricht zu senden.
            return Task.FromResult(0);
        }
    }

    // Konfigurieren des in dieser Anwendung verwendeten Anwendungsbenutzer-Managers. UserManager wird in ASP.NET Identity definiert und von der Anwendung verwendet.
    // Change PrimaryKey of identity package to int [public class ApplicationUserManager : UserManager<ApplicationUser>] //KrabsJ
    public class ApplicationUserManager : UserManager<ApplicationUser, int>
    {
        /*Change PrimaryKey of identity package to int //KrabsJ
        public ApplicationUserManager(IUserStore<ApplicationUser> store)
            : base(store)
        {
        }
        */
        public ApplicationUserManager(IUserStore<ApplicationUser, int> store)
            : base(store)
        {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options,
            IOwinContext context)
        {
            //Change PrimaryKey of identity package to int [var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));] //KrabsJ
            var manager = new ApplicationUserManager(new CustomUserStore(context.Get<ApplicationDbContext>()));
            // Konfigurieren der Überprüfungslogik für Benutzernamen.
            //Change PrimaryKey of identity package to int [manager.UserValidator = new UserValidator<ApplicationUser>(manager)] //KrabsJ
            manager.UserValidator = new UserValidator<ApplicationUser, int>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };

            // Konfigurieren der Überprüfungslogik für Kennwörter.
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true
            };

            // Standardeinstellungen für Benutzersperren konfigurieren
            manager.UserLockoutEnabledByDefault = true;
            manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
            manager.MaxFailedAccessAttemptsBeforeLockout = 5;

            // Registrieren von Anbietern für zweistufige Authentifizierung. Diese Anwendung verwendet telefonische und E-Mail-Nachrichten zum Empfangen eines Codes zum Überprüfen des Benutzers.
            // Sie können Ihren eigenen Anbieter erstellen und hier einfügen.
            /* Change PrimaryKey of identity package to int //KrabsJ 
            manager.RegisterTwoFactorProvider("Telefoncode", new PhoneNumberTokenProvider<ApplicationUser>
            {
                MessageFormat = "Ihr Sicherheitscode lautet {0}"
            });
            manager.RegisterTwoFactorProvider("E-Mail-Code", new EmailTokenProvider<ApplicationUser>
            {
                Subject = "Sicherheitscode",
                BodyFormat = "Ihr Sicherheitscode lautet {0}"
            });
            */
            manager.RegisterTwoFactorProvider("Telefoncode", new PhoneNumberTokenProvider<ApplicationUser, int>
            {
                MessageFormat = "Ihr Sicherheitscode lautet {0}"
            });
            manager.RegisterTwoFactorProvider("E-Mail-Code", new EmailTokenProvider<ApplicationUser, int>
            {
                Subject = "Sicherheitscode",
                BodyFormat = "Ihr Sicherheitscode lautet {0}"
            });
            manager.EmailService = new EmailService();
            manager.SmsService = new SmsService();
            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
                manager.UserTokenProvider =
                    new DataProtectorTokenProvider<ApplicationUser, int>(
                        dataProtectionProvider.Create("ASP.NET Identity"));
            return manager;
        }
    }

    // Anwendungsanmelde-Manager konfigurieren, der in dieser Anwendung verwendet wird.
    //Change Primary Key of identity package to int [public class ApplicationSignInManager : SignInManager<ApplicationUser, string>] //KrabsJ
    public class ApplicationSignInManager : SignInManager<ApplicationUser, int>
    {
        public ApplicationSignInManager(ApplicationUserManager userManager,
            IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }

        public override Task<ClaimsIdentity> CreateUserIdentityAsync(ApplicationUser user)
        {
            return user.GenerateUserIdentityAsync((ApplicationUserManager) UserManager);
        }

        public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options,
            IOwinContext context)
        {
            return new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(),
                context.Authentication);
        }
    }

    // creating a custom ApplicationRoleManager to be able to use the CustomRoleStore for managing roles [KrabsJ]
    public class ApplicationRoleManager : RoleManager<CustomRole, int>
    {
        public ApplicationRoleManager(IRoleStore<CustomRole, int> roleStore) : base(roleStore)
        {
        }

        public static ApplicationRoleManager Create(IdentityFactoryOptions<ApplicationRoleManager> options,
            IOwinContext context)
        {
            return new ApplicationRoleManager(
                new RoleStore<CustomRole, int, CustomUserRole>(context.Get<ApplicationDbContext>()));
        }
    }
}