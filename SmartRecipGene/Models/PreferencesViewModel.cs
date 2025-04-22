public class PreferencesViewModel
{
    public List<string> DietaryRestrictions { get; set; } = new List<string>();
    public List<string> PreferredCuisines { get; set; } = new List<string>();
    public List<string> MealTypes { get; set; } = new List<string>();
    public bool HasSetPreferences { get; set; }
}