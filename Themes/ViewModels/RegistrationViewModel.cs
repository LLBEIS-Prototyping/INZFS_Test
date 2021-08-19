﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace INZFS.Theme.ViewModels
{
    public class MyAccountViewModel
    {
        public string EmailAddress { get; set; }
        public string PhoneNumber { get; set; }
        public string ReturnUrl { get; set; }
        public bool IsAuthenticatorEnabled { get; set; }
        public bool IsSmsEnabled { get; set; }
    }

    public class RegistrationViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Compare("Email")]
        public string ConfirmEmail { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Compare("Password")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

    }

    public class RegistrationSuccessViewModel
    {
        public string Email { get; set; }
        public bool VerificationRequired { get; set; }
    }
}
