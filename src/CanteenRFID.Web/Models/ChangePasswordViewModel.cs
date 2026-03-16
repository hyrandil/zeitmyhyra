using System.ComponentModel.DataAnnotations;

namespace CanteenRFID.Web.Models;

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Aktuelles Passwort")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Neues Passwort")]
    [MinLength(8, ErrorMessage = "Mindestens 8 Zeichen erforderlich.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Neues Passwort bestätigen")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwörter stimmen nicht überein.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
