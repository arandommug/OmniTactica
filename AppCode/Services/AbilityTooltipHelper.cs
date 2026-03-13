using OmniTactica.AppCode.Models.Core;
using System.Text.Json;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Helper service for generating tooltips for weapon abilities based on rules definitions.
    /// </summary>
    public static class AbilityTooltipHelper
    {
        /// <summary>
        /// Gets a tooltip description for a weapon ability.
        /// </summary>
        public static string GetTooltip(WeaponAbility ability, AbilityRulesService? rulesService = null)
        {
            if (ability == null) return string.Empty;

            // Try to get the rule definition
            var rule = rulesService?.GetRule(ability.Id);

            if (rule != null)
            {
                return FormatRuleTooltip(rule, ability);
            }

            // Fallback to basic tooltip based on ability ID
            return GetFallbackTooltip(ability);
        }

        /// <summary>
        /// Gets tooltip for an ability name string (legacy support).
        /// </summary>
        public static string GetTooltipFromName(string abilityName, AbilityRulesService? rulesService = null)
        {
            if (string.IsNullOrWhiteSpace(abilityName)) return string.Empty;

            // Parse the ability name to extract ID and value
            var (id, value) = ParseAbilityName(abilityName);

            var rule = rulesService?.GetRule(id);
            if (rule != null)
            {
                return FormatRuleTooltipFromName(rule, abilityName, value);
            }

            return GetFallbackTooltipFromName(abilityName);
        }

        private static string FormatRuleTooltip(AbilityRuleDefinition rule, WeaponAbility ability)
        {
            return BuildPrettyTooltip(rule, ability.DisplayName, ability.Value, ability.Parameter, ability.IsCustom);
        }

        private static string FormatRuleTooltipFromName(AbilityRuleDefinition rule, string abilityName, string? value)
        {
            int? numValue = int.TryParse(value, out var v) ? v : null;
            return BuildPrettyTooltip(rule, abilityName, numValue, null, false);
        }

        private static string BuildPrettyTooltip(AbilityRuleDefinition rule, string displayName, int? value, string? parameter, bool isCustom)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<div style=\"text-align:left;min-width:220px;max-width:320px;word-wrap:break-word;overflow-wrap:break-word;\">");

            // Header
            sb.Append("<div style=\"font-weight:700;font-size:1.1em;border-bottom:1px solid rgba(255,255,255,0.3);padding-bottom:4px;margin-bottom:8px;\">");
            sb.Append(System.Web.HttpUtility.HtmlEncode(displayName));
            sb.Append("</div>");

            // Details table
            sb.Append("<table style=\"width:100%;font-size:0.85em;border-collapse:collapse;table-layout:fixed;\">");

            // Value (if present)
            if (value.HasValue)
            {
                sb.Append("<tr>");
                sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;\">Value</td>");
                sb.Append($"<td style=\"padding:2px 0;font-weight:600;color:#ffc107;\">{value.Value}</td>");
                sb.Append("</tr>");
            }

            // Parameter (if present)
            if (!string.IsNullOrEmpty(parameter))
            {
                sb.Append("<tr>");
                sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;\">Parameter</td>");
                sb.Append($"<td style=\"padding:2px 0;\">{System.Web.HttpUtility.HtmlEncode(parameter)}</td>");
                sb.Append("</tr>");
            }

            // Trigger
            if (!string.IsNullOrEmpty(rule.Trigger))
            {
                sb.Append("<tr>");
                sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;width:70px;\">Timing</td>");
                sb.Append($"<td style=\"padding:2px 0;word-wrap:break-word;\">{FormatTrigger(rule.Trigger)}</td>");
                sb.Append("</tr>");
            }

            // Condition
            if (rule.Condition.ValueKind != JsonValueKind.Undefined && rule.Condition.ValueKind != JsonValueKind.Null)
            {
                var conditionType = rule.Condition.TryGetProperty("type", out var typeElem) 
                    ? typeElem.GetString() ?? "" 
                    : "";

                sb.Append("<tr>");
                sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;width:70px;\">Condition</td>");
                sb.Append($"<td style=\"padding:2px 0;word-wrap:break-word;\">{FormatCondition(conditionType, rule.Condition)}</td>");
                sb.Append("</tr>");
            }

            // Effect
            if (rule.Effect.ValueKind != JsonValueKind.Undefined && rule.Effect.ValueKind != JsonValueKind.Null)
            {
                var effectType = rule.Effect.TryGetProperty("type", out var typeElem) 
                    ? typeElem.GetString() ?? "" 
                    : "";
                var effectValue = GetJsonValue(rule.Effect, "value");

                // Replace X placeholder with actual value
                if (effectValue == "X" && value.HasValue)
                {
                    effectValue = value.Value.ToString();
                }

                sb.Append("<tr>");
                sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;width:70px;\">Effect</td>");
                sb.Append($"<td style=\"padding:2px 0;font-weight:600;color:#20c997;word-wrap:break-word;\">{FormatEffect(effectType, effectValue, null)}</td>");
                sb.Append("</tr>");
            }

            // Custom badge
            if (isCustom)
            {
                sb.Append("<tr>");
                sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;\">Source</td>");
                sb.Append("<td style=\"padding:2px 0;\"><span style=\"background:#6c757d;color:#fff;padding:1px 6px;border-radius:3px;font-size:0.8em;\">Custom</span></td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");
            sb.Append("</div>");

            return sb.ToString();
        }

        private static string GetFallbackTooltip(WeaponAbility ability)
        {
            var description = ability.Id switch
            {
                "assault" => "Can be fired after advancing",
                "blast" => "Extra attacks based on target size: 5-9 models → +1, 10+ models → +2",
                "devastating_wounds" => "Critical wounds become mortal wounds",
                "hazardous" => "Risk of self-damage on use",
                "heavy" => "+1 to hit if unit remained stationary",
                "ignores_cover" => "Target receives no benefit from cover",
                "indirect_fire" => "Can target units not visible to the firer",
                "lethal_hits" => "Critical hits automatically wound",
                "pistol_shoot_in_melee" => "Can be fired while in engagement range",
                "precision" => "Can allocate attacks to characters",
                "torrent" => "Automatically hits (no hit roll required)",
                "twin_linked" => "Re-roll failed wound rolls",
                "rapid_fire" => $"Gains +{ability.Value ?? 0} attacks when within half range",
                "melta" => $"+{ability.Value ?? 6} damage when within half range",
                "sustained_hits" => $"Each critical hit scores {ability.Value ?? 1} additional hit(s)",
                _ when ability.Id.StartsWith("anti_") => $"Critical wound on {ability.Value ?? 4}+ against targets with this keyword",
                _ => ability.IsCustom ? "Custom ability" : "Weapon ability"
            };

            return BuildFallbackPrettyTooltip(ability.DisplayName, ability.Id, ability.Value, ability.Parameter, ability.IsCustom, description);
        }

        private static string GetFallbackTooltipFromName(string abilityName)
        {
            var lower = abilityName.ToLowerInvariant();

            string description;
            if (lower.Contains("assault")) description = "Can be fired after advancing";
            else if (lower.Contains("blast")) description = "Extra attacks based on target unit size";
            else if (lower.Contains("devastating wounds")) description = "Critical wounds become mortal wounds";
            else if (lower.Contains("hazardous")) description = "Risk of self-damage on use";
            else if (lower.Contains("heavy")) description = "+1 to hit if unit remained stationary";
            else if (lower.Contains("ignores cover")) description = "Target receives no benefit from cover";
            else if (lower.Contains("indirect fire")) description = "Can target units not visible";
            else if (lower.Contains("lethal hits")) description = "Critical hits automatically wound";
            else if (lower.Contains("pistol")) description = "Can be fired while in engagement range";
            else if (lower.Contains("precision")) description = "Can allocate attacks to characters";
            else if (lower.Contains("torrent")) description = "Automatically hits (no hit roll required)";
            else if (lower.Contains("twin-linked")) description = "Re-roll failed wound rolls";
            else if (lower.Contains("rapid fire")) description = "Gains extra attacks when within half range";
            else if (lower.Contains("melta")) description = "Extra damage when within half range";
            else if (lower.Contains("sustained hits")) description = "Each critical hit scores additional hits";
            else if (lower.Contains("anti-")) description = "Critical wound on special roll against matching targets";
            else description = abilityName;

            return BuildFallbackPrettyTooltip(abilityName, null, null, null, false, description);
        }

        private static string BuildFallbackPrettyTooltip(string displayName, string? id, int? value, string? parameter, bool isCustom, string description)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<div style=\"text-align:left;min-width:200px;max-width:300px;word-wrap:break-word;overflow-wrap:break-word;\">");

            // Header
            sb.Append("<div style=\"font-weight:700;font-size:1.1em;border-bottom:1px solid rgba(255,255,255,0.3);padding-bottom:4px;margin-bottom:8px;\">");
            sb.Append(System.Web.HttpUtility.HtmlEncode(displayName));
            sb.Append("</div>");

            // Description
            sb.Append($"<div style=\"margin-bottom:6px;color:#20c997;font-weight:600;line-height:1.4;\">{System.Web.HttpUtility.HtmlEncode(description)}</div>");

            // Additional fields if available
            var hasDetails = value.HasValue || !string.IsNullOrEmpty(parameter);
            if (hasDetails)
            {
                sb.Append("<table style=\"width:100%;font-size:0.85em;border-collapse:collapse;table-layout:fixed;\">");

                if (value.HasValue)
                {
                    sb.Append("<tr>");
                    sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;\">Value</td>");
                    sb.Append($"<td style=\"padding:2px 0;font-weight:600;color:#ffc107;\">{value.Value}</td>");
                    sb.Append("</tr>");
                }

                if (!string.IsNullOrEmpty(parameter))
                {
                    sb.Append("<tr>");
                    sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;\">Parameter</td>");
                    sb.Append($"<td style=\"padding:2px 0;\">{System.Web.HttpUtility.HtmlEncode(parameter)}</td>");
                    sb.Append("</tr>");
                }

                if (isCustom)
                {
                    sb.Append("<tr>");
                    sb.Append("<td style=\"color:#adb5bd;padding:2px 8px 2px 0;vertical-align:top;white-space:nowrap;\">Source</td>");
                    sb.Append("<td style=\"padding:2px 0;\"><span style=\"background:#6c757d;color:#fff;padding:1px 6px;border-radius:3px;font-size:0.8em;\">Custom</span></td>");
                    sb.Append("</tr>");
                }

                sb.Append("</table>");
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private static (string id, string? value) ParseAbilityName(string abilityName)
        {
            var lower = abilityName.ToLowerInvariant();

            if (lower.Contains("rapid fire"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(abilityName, @"(\d+)");
                return ("rapid_fire", match.Success ? match.Groups[1].Value : null);
            }
            if (lower.Contains("melta"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(abilityName, @"(\d+)");
                return ("melta", match.Success ? match.Groups[1].Value : null);
            }
            if (lower.Contains("sustained hits"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(abilityName, @"(\d+)");
                return ("sustained_hits", match.Success ? match.Groups[1].Value : null);
            }
            if (lower.Contains("anti-"))
            {
                var keyword = System.Text.RegularExpressions.Regex.Match(abilityName, @"anti-(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var threshold = System.Text.RegularExpressions.Regex.Match(abilityName, @"(\d+)\+");
                var id = keyword.Success ? $"anti_{keyword.Groups[1].Value.ToLowerInvariant()}" : "anti_unknown";
                return (id, threshold.Success ? threshold.Groups[1].Value : null);
            }

            // Map common names to IDs
            return lower switch
            {
                var s when s.Contains("assault") => ("assault", null),
                var s when s.Contains("blast") => ("blast", null),
                var s when s.Contains("devastating wounds") => ("devastating_wounds", null),
                var s when s.Contains("hazardous") => ("hazardous", null),
                var s when s.Contains("heavy") => ("heavy", null),
                var s when s.Contains("ignores cover") => ("ignores_cover", null),
                var s when s.Contains("indirect fire") => ("indirect_fire", null),
                var s when s.Contains("lethal hits") => ("lethal_hits", null),
                var s when s.Contains("pistol") => ("pistol_shoot_in_melee", null),
                var s when s.Contains("precision") => ("precision", null),
                var s when s.Contains("torrent") => ("torrent", null),
                var s when s.Contains("twin-linked") => ("twin_linked", null),
                _ => ("unknown", null)
            };
        }

        private static string FormatTrigger(string trigger)
        {
            return trigger switch
            {
                "attack_start" => "At the start of the attack",
                "before_hit_roll" => "Before making hit rolls",
                "after_hit_roll" => "After making hit rolls",
                "before_wound_roll" => "Before making wound rolls",
                "after_wound_roll" => "After making wound rolls",
                "damage_roll" => "When rolling damage",
                "after_wound" => "After wounding",
                "after_damage" => "After damage is applied",
                "before_damage_applied" => "Before damage is applied",
                "unit_selected_to_shoot" => "When unit is selected to shoot",
                "charge_phase" => "During the Charge phase",
                "fight_phase" => "During the Fight phase",
                "model_destroyed" => "When a model is destroyed",
                "objective_control" => "During Objective Control",
                "save_roll" => "When making save rolls",
                _ => char.ToUpper(trigger[0]) + trigger[1..].Replace("_", " ")
            };
        }

        private static string FormatCondition(string conditionType, JsonElement condition)
        {
            // Extract value if present for conditions like hit_roll_equals
            var condValue = condition.TryGetProperty("value", out var valElem)
                ? (valElem.ValueKind == JsonValueKind.Number ? valElem.GetInt32().ToString() : valElem.GetString() ?? "")
                : "";

            return conditionType switch
            {
                "always" => "Always active",
                "critical_hit" => "Critical hit (unmodified 6 to hit)",
                "critical_wound" => "Critical wound (unmodified 6 to wound)",
                "target_within_half_range" => "Within half range",
                "target_unit_size_5_plus" => "Target has 5+ models",
                "target_unit_size_10_plus" => "Target has 10+ models",
                "target_is_vehicle" => "vs VEHICLE",
                "target_is_monster" => "vs MONSTER",
                "target_has_character" => "vs CHARACTER",
                "within_engagement_range" => "In engagement range",
                "unit_remained_stationary" => "Unit remained stationary",
                "hit_roll_equals" => $"Hit roll equals {condValue}",
                "wound_roll_equals" => $"Wound roll equals {condValue}",
                "unit_charged_this_turn" => "Unit charged this turn",
                "unit_advanced" => "Unit advanced",
                "unit_fell_back" => "Unit fell back",
                "attacker_destroyed_model" => "Attacker destroyed a model",
                "within_objective_range" => "Within range of an objective",
                "target_in_cover" => "Target is in cover",
                _ => char.ToUpper(conditionType[0]) + conditionType[1..].Replace("_", " ")
            };
        }

        private static string FormatEffect(string effectType, string effectValue, WeaponAbility? ability)
        {
            return effectType switch
            {
                "add_attacks" => $"+{effectValue} attack(s)",
                "add_hits" => $"Each critical hit generates {effectValue} additional hit(s)",
                "auto_hit" => "Automatically hits (no hit roll required)",
                "auto_wound" => "Automatically wounds (no wound roll required)",
                "convert_damage_to_mortal" => "Convert critical wounds to mortal wounds",
                "reroll_wounds" => "Re-roll wound rolls",
                "reroll_hits" => "Re-roll hit rolls",
                "reroll_hit" => "Re-roll the hit roll",
                "reroll_wound" => "Re-roll the wound roll",
                "add_damage" => $"+{effectValue} damage",
                "add_hit_modifier" => $"{(int.TryParse(effectValue, out var hm) && hm >= 0 ? "+" : "")}{effectValue} to hit rolls",
                "add_wound_modifier" => $"{(int.TryParse(effectValue, out var wm) && wm >= 0 ? "+" : "")}{effectValue} to wound rolls",
                "add_save_modifier" => $"+{effectValue} to save rolls",
                "critical_wound_on" => $"Critical wound on {effectValue}+",
                "allow_shooting" => "Allows shooting",
                "allow_character_allocation" => "Can allocate attacks to characters",
                "allow_charge" => "Allows charging",
                "ignore_wounds_on" => $"Feel No Pain {effectValue}+ (ignore wound on {effectValue}+)",
                "reduce_damage" => $"Reduce incoming damage by {effectValue} (minimum 1)",
                "halve_damage" => "Halve incoming damage (rounding up, minimum 1)",
                "fight_first" => "Fights first in the Fight phase",
                "allow_final_attack" => "Can fight on death before being removed",
                "add_oc" => $"+{effectValue} to Objective Control",
                _ => char.ToUpper(effectType[0]) + effectType[1..].Replace("_", " ") + (!string.IsNullOrEmpty(effectValue) ? $" ({effectValue})" : "")
            };
        }

        /// <summary>
        /// Helper method to extract value from JSON element that can be either string or number.
        /// </summary>
        private static string GetJsonValue(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var valueElem))
            {
                return string.Empty;
            }

            return valueElem.ValueKind switch
            {
                JsonValueKind.String => valueElem.GetString() ?? string.Empty,
                JsonValueKind.Number => valueElem.GetInt32().ToString(),
                _ => string.Empty
            };
        }
    }
}
