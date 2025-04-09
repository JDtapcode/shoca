using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.TokenModels
{
    public class RefreshTokenModel
    {
        [Required(ErrorMessage = "Access token is required")]
        public string AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }
}
