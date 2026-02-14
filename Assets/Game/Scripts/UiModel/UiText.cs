using System;

namespace Game.UiModel {
    // Shared presentation copy; callers supply authored ability names.
    public static class UiText {
        public const string AbilityKeys = "SPACE / E";
        public static string Controls(string abilityName) =>
            $"WASD / стрелки — движение     ЛКМ / J — атака ближайшего врага     Space / E — {abilityName}";
        public static string RequireAbilityName(string name) => !string.IsNullOrWhiteSpace(name)
            ? name : throw new ArgumentException("Ability display name is required.", nameof(name));
    }
}
