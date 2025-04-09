﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.AccountModels
{
    public class AccountChangePasswordModel
    {
        [Required(ErrorMessage = "Old password is required")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Old password must be from 8 to 128 characters")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "New password must be from 8 to 128 characters")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Confirm password must be from 8 to 128 characters")]
        [Compare("NewPassword", ErrorMessage = "New password and confirm password does not match")]
        public string ConfirmPassword { get; set; }
    }
}
