﻿using Repositories.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.AccountModels
{
    public class AccountUpdateModel
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name must be no more than 50 characters")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name must be no more than 50 characters")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [EnumDataType(typeof(Gender), ErrorMessage = "Invalid gender")]
        public Gender Gender { get; set; }

        [Required(ErrorMessage = "Date of Birth is required")]
        public DateTime DateOfBirth { get; set; }

        public string? Address { get; set; }
        public string? AvatarUrl { get; set; }
        public string? PersonalWebsiteUrl { get; set; }
        public string? PortfolioUrl { get; set; }

        [Required(ErrorMessage = "Phone number is required"), Phone(ErrorMessage = "Invalid phone format")]
        [StringLength(15, ErrorMessage = "Phone number must be no more than 15 characters")]
        public string PhoneNumber { get; set; }
        public Role Role { get; set; }
    }
}
