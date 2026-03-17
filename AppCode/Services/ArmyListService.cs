using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Maui.Storage;
using OmniTactica.AppCode.Helpers;
using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Models.Rules;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Services
{
    public class ArmyListService
    {
        private const string StorageKey = "omnitactica_army_list";
        private readonly WahaDataService _data;
        private ArmyListCollection _collection = new();

        public event Action? OnListChanged;

        public ArmyListService(WahaDataService data)
        {
            _data = data;
            LoadArmyLists();
        }

        public ArmyList GetArmyList() => EnsureActiveList();

        public List<ArmyList> GetArmyLists() => _collection.Lists
            .OrderByDescending(list => list.UpdatedUtc)
            .ToList();

        public async Task<ArmyList> CreateListAsync(string? name = null)
        {
            var list = CreateDefaultList(name);
            _collection.Lists.Insert(0, list);
            _collection.ActiveListId = list.Id;
            await PersistAsync();
            return list;
        }

        public async Task<ArmyList?> SetActiveListAsync(string listId)
        {
            var list = _collection.Lists.FirstOrDefault(existingList => existingList.Id == listId);
            if (list == null)
            {
                return null;
            }

            _collection.ActiveListId = list.Id;
            await PersistAsync();
            return list;
        }

        public async Task<ArmyList> DuplicateActiveListAsync()
        {
            var source = EnsureActiveList();
            var duplicate = CloneList(source, GenerateUniqueListName($"{source.Name} Copy"));
            _collection.Lists.Insert(0, duplicate);
            _collection.ActiveListId = duplicate.Id;
            await PersistAsync();
            return duplicate;
        }

        public async Task DeleteActiveListAsync()
        {
            var active = EnsureActiveList();
            _collection.Lists.RemoveAll(list => list.Id == active.Id);

            EnsureActiveList();
            await PersistAsync();
        }

        public async Task SetListNameAsync(string? name)
        {
            var armyList = EnsureActiveList();
            armyList.Name = string.IsNullOrWhiteSpace(name) ? GenerateUniqueListName("New Army List") : name.Trim();
            await PersistAsync();
        }

        public async Task SetPointsLimitAsync(int pointsLimit)
        {
            var armyList = EnsureActiveList();
            armyList.PointsLimit = Math.Max(500, pointsLimit);
            await PersistAsync();
        }

        public async Task SetFactionAsync(string factionId, string factionName)
        {
            var armyList = EnsureActiveList();
            if (string.Equals(armyList.FactionId, factionId, StringComparison.OrdinalIgnoreCase))
            {
                armyList.FactionName = factionName;
                await PersistAsync();
                return;
            }

            armyList.FactionId = factionId;
            armyList.FactionName = factionName;
            armyList.DetachmentId = null;
            armyList.DetachmentName = string.Empty;
            armyList.Units.Clear();
            await PersistAsync();
        }

        public async Task SetDetachmentAsync(int? detachmentId, string? detachmentName, IEnumerable<Enhancement>? availableEnhancements = null)
        {
            var armyList = EnsureActiveList();
            armyList.DetachmentId = detachmentId;
            armyList.DetachmentName = detachmentName ?? string.Empty;

            foreach (var unit in armyList.Units)
            {
                NormalizeEnhancementEligibility(unit);
            }

            if (availableEnhancements != null)
            {
                var validIds = availableEnhancements.Select(e => e.Id).ToHashSet();
                foreach (var unit in armyList.Units.Where(unit => unit.EnhancementId.HasValue && !validIds.Contains(unit.EnhancementId.Value)))
                {
                    unit.EnhancementId = null;
                    unit.EnhancementName = string.Empty;
                    unit.EnhancementCost = 0;
                }
            }

            await PersistAsync();
        }

        public async Task<ArmyListUnit> AddUnitAsync(Datasheet datasheet, int? detachmentId = null)
        {
            var armyList = EnsureActiveList();
            var detail = await _data.GetDatasheetDetailAsync(datasheet.Id, detachmentId);
            if (detail == null)
            {
                throw new InvalidOperationException($"Datasheet {datasheet.Id} could not be loaded.");
            }

            var costOptions = detail.Costs
                .Select(cost => new ArmyListCostOption
                {
                    Line = cost.Line,
                    Description = string.IsNullOrWhiteSpace(cost.Description)
                        ? "Base unit"
                        : HtmlUtility.ConvertHtmlToPlainText(cost.Description).Trim(),
                    Cost = cost.Cost
                })
                .GroupBy(cost => new { cost.Line, cost.Description, cost.Cost })
                .Select(group => group.First())
                .OrderBy(cost => cost.Cost)
                .ThenBy(cost => cost.Description)
                .ToList();

            var defaultCost = costOptions.FirstOrDefault();
            var keywords = detail.Keywords
                .Concat(detail.FactionKeywords)
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(keyword => keyword)
                .ToList();

            var unit = new ArmyListUnit
            {
                DatasheetId = datasheet.Id,
                DatasheetName = datasheet.Name,
                Role = datasheet.Role,
                Keywords = keywords,
                Composition = CreateSelectedCompositionDescriptions(detail, defaultCost?.Description),
                Models = CreateUnitModels(detail, defaultCost?.Description),
                CostOptions = costOptions,
                SelectedCostLine = defaultCost?.Line,
                SelectedCostDescription = defaultCost?.Description ?? "Base unit",
                SelectedPoints = defaultCost?.Cost ?? datasheet.PointsCost ?? 0,
                CanTakeEnhancement = CanUnitTakeEnhancement(keywords)
            };

            NormalizeEnhancementEligibility(unit);
            ApplyAutomaticCostSelection(unit);

            armyList.Units.Add(unit);
            await PersistAsync();
            return unit;
        }

        public async Task DuplicateUnitAsync(string unitId)
        {
            var armyList = EnsureActiveList();
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                return;
            }

            var duplicate = CloneUnit(unit);

            armyList.Units.Add(duplicate);
            await PersistAsync();
        }

        public async Task RemoveUnitAsync(string unitId)
        {
            var armyList = EnsureActiveList();
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                return;
            }

            armyList.Units.Remove(unit);
            await PersistAsync();
        }

        public async Task SetUnitCostAsync(string unitId, int selectedCostLine)
        {
            var armyList = EnsureActiveList();
            var unit = FindUnit(unitId);
            var cost = unit?.CostOptions.FirstOrDefault(option => option.Line == selectedCostLine);
            if (unit == null || cost == null)
            {
                return;
            }

            unit.SelectedCostLine = cost.Line;
            unit.SelectedCostDescription = cost.Description;
            unit.SelectedPoints = cost.Cost;

            var detail = await _data.GetDatasheetDetailAsync(unit.DatasheetId, armyList.DetachmentId);
            if (detail != null)
            {
                unit.Models = CreateUnitModels(detail, cost.Description);
                unit.Composition = CreateSelectedCompositionDescriptions(detail, cost.Description);
            }

            await PersistAsync();
        }

        public async Task SetModelQuantityAsync(string unitId, string modelId, int quantity)
        {
            var unit = FindUnit(unitId);
            var model = unit?.Models.FirstOrDefault(existingModel => existingModel.Id == modelId);
            if (unit == null || model == null)
            {
                return;
            }

            model.Quantity = Math.Clamp(quantity, model.MinQuantity, Math.Max(model.MinQuantity, model.MaxQuantity));
            ApplyAutomaticCostSelection(unit);
            unit.Composition = CreateCompositionDescriptionsFromModels(unit.Models);
            await PersistAsync();
        }

        public async Task AddWeaponAsync(string unitId, string modelId, string weaponName)
        {
            if (string.IsNullOrWhiteSpace(weaponName))
            {
                return;
            }

            var armyList = EnsureActiveList();
            var unit = FindUnit(unitId);
            var model = unit?.Models.FirstOrDefault(existingModel => existingModel.Id == modelId);
            if (unit == null || model == null)
            {
                return;
            }

            var detail = await _data.GetDatasheetDetailAsync(unit.DatasheetId, armyList.DetachmentId);
            var wargear = detail?.Wargear.FirstOrDefault(option =>
                option.Name.Equals(weaponName, StringComparison.OrdinalIgnoreCase));

            if (wargear == null)
            {
                return;
            }

            var existingWeapon = model.Weapons.FirstOrDefault(existing =>
                existing.Name.Equals(wargear.Name, StringComparison.OrdinalIgnoreCase));

            if (existingWeapon != null)
            {
                existingWeapon.Quantity++;
                existingWeapon.IsSelected = true;
            }
            else
            {
                model.Weapons.Add(CreateArmyListWeaponFromWargear(wargear));
            }

            await PersistAsync();
        }

        public async Task RemoveWeaponAsync(string unitId, string modelId, string weaponId)
        {
            var model = FindModel(unitId, modelId);
            var weapon = FindWeapon(unitId, modelId, weaponId);
            if (model == null || weapon == null)
            {
                return;
            }

            model.Weapons.Remove(weapon);
            await PersistAsync();
        }

        public async Task SetWeaponQuantityAsync(string unitId, string modelId, string weaponId, int quantity)
        {
            var weapon = FindWeapon(unitId, modelId, weaponId);
            if (weapon == null)
            {
                return;
            }

            weapon.Quantity = Math.Max(1, quantity);
            await PersistAsync();
        }

        public async Task SetWeaponSelectedAsync(string unitId, string modelId, string weaponId, bool isSelected)
        {
            var weapon = FindWeapon(unitId, modelId, weaponId);
            if (weapon == null)
            {
                return;
            }

            weapon.IsSelected = isSelected;
            await PersistAsync();
        }

        public async Task SetUnitEnhancementAsync(string unitId, Enhancement? enhancement)
        {
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                return;
            }

            NormalizeEnhancementEligibility(unit);
            if (!unit.CanTakeEnhancement)
            {
                enhancement = null;
            }

            unit.EnhancementId = enhancement?.Id;
            unit.EnhancementName = enhancement?.Name ?? string.Empty;
            unit.EnhancementCost = enhancement?.Cost ?? 0;
            await PersistAsync();
        }

        public async Task SetUnitNotesAsync(string unitId, string? notes)
        {
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                return;
            }

            unit.Notes = notes?.Trim() ?? string.Empty;
            await PersistAsync();
        }

        public async Task ResetAsync()
        {
            var active = EnsureActiveList();
            var reset = CreateDefaultList(active.Name);
            reset.Id = active.Id;
            reset.CreatedUtc = active.CreatedUtc;

            var index = _collection.Lists.FindIndex(list => list.Id == active.Id);
            if (index >= 0)
            {
                _collection.Lists[index] = reset;
            }

            _collection.ActiveListId = reset.Id;
            await PersistAsync();
        }

        private void LoadArmyLists()
        {
            try
            {
                var json = Preferences.Get(StorageKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _collection = JsonSerializer.Deserialize<ArmyListCollection>(json) ?? new ArmyListCollection();

                    if (_collection.Lists.Count == 0)
                    {
                        var legacyList = JsonSerializer.Deserialize<ArmyList>(json);
                        if (legacyList != null)
                        {
                            if (string.IsNullOrWhiteSpace(legacyList.Id))
                            {
                                legacyList.Id = Guid.NewGuid().ToString();
                            }

                            _collection.Lists.Add(legacyList);
                            _collection.ActiveListId = legacyList.Id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArmyListService] Error loading army list: {ex.Message}");
                _collection = new ArmyListCollection();
            }

            EnsureActiveList();
        }

        private static bool CanUnitTakeEnhancement(IEnumerable<string> keywords)
        {
            var keywordSet = keywords
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return keywordSet.Contains("Character") && !keywordSet.Contains("Epic Hero");
        }

        private static void NormalizeEnhancementEligibility(ArmyListUnit unit)
        {
            unit.CanTakeEnhancement = CanUnitTakeEnhancement(unit.Keywords);
            if (unit.CanTakeEnhancement)
            {
                return;
            }

            unit.EnhancementId = null;
            unit.EnhancementName = string.Empty;
            unit.EnhancementCost = 0;
        }

        private Task PersistAsync()
        {
            try
            {
                EnsureActiveList().UpdatedUtc = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(_collection);
                Preferences.Set(StorageKey, json);
                OnListChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArmyListService] Error saving army list: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private ArmyList EnsureActiveList()
        {
            if (_collection.Lists.Count == 0)
            {
                var list = CreateDefaultList();
                _collection.Lists.Add(list);
                _collection.ActiveListId = list.Id;
                return list;
            }

            var active = _collection.Lists.FirstOrDefault(list => list.Id == _collection.ActiveListId);
            if (active != null)
            {
                return active;
            }

            active = _collection.Lists
                .OrderByDescending(list => list.UpdatedUtc)
                .First();

            _collection.ActiveListId = active.Id;
            return active;
        }

        private ArmyList CreateDefaultList(string? name = null)
        {
            var resolvedName = string.IsNullOrWhiteSpace(name)
                ? GenerateUniqueListName("New Army List")
                : name.Trim();

            return new ArmyList
            {
                Name = resolvedName,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private ArmyList CloneList(ArmyList source, string? name = null)
        {
            return new ArmyList
            {
                Name = string.IsNullOrWhiteSpace(name) ? source.Name : name,
                FactionId = source.FactionId,
                FactionName = source.FactionName,
                DetachmentId = source.DetachmentId,
                DetachmentName = source.DetachmentName,
                PointsLimit = source.PointsLimit,
                Units = source.Units.Select(CloneUnit).ToList(),
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private ArmyListUnit CloneUnit(ArmyListUnit unit)
        {
            return new ArmyListUnit
            {
                DatasheetId = unit.DatasheetId,
                DatasheetName = unit.DatasheetName,
                Role = unit.Role,
                Keywords = new List<string>(unit.Keywords),
                Composition = new List<string>(unit.Composition),
                Models = CloneModels(unit.Models),
                CostOptions = CloneCostOptions(unit.CostOptions),
                SelectedCostLine = unit.SelectedCostLine,
                SelectedCostDescription = unit.SelectedCostDescription,
                SelectedPoints = unit.SelectedPoints,
                CanTakeEnhancement = unit.CanTakeEnhancement,
                EnhancementId = unit.EnhancementId,
                EnhancementName = unit.EnhancementName,
                EnhancementCost = unit.EnhancementCost,
                Notes = unit.Notes
            };
        }

        private static List<ArmyListCostOption> CloneCostOptions(IEnumerable<ArmyListCostOption> costOptions)
        {
            return costOptions.Select(cost => new ArmyListCostOption
            {
                Line = cost.Line,
                Description = cost.Description,
                Cost = cost.Cost
            }).ToList();
        }

        private List<ArmyListUnitModel> CreateUnitModels(DatasheetDetail detail, string? selectedCostDescription = null)
        {
            var compositions = DatasheetParsingHelper.ParseUnitCompositionOptions(detail.UnitComposition);
            var selectedComposition = DatasheetParsingHelper.SelectCompositionOption(compositions, selectedCostDescription);

            if (selectedComposition.Count == 0 && detail.Models.Any())
            {
                var profile = detail.Models.First();
                selectedComposition.Add(new DatasheetCompositionEntry(profile.Name, 1, 1));
            }

            var models = new List<ArmyListUnitModel>();
            foreach (var entry in selectedComposition)
            {
                var modelName = entry.Name;
                var minQuantity = entry.MinQuantity;
                var maxQuantity = entry.MaxQuantity;
                var modelProfile = DatasheetParsingHelper.FindModelProfile(detail, modelName);
                if (modelProfile == null)
                {
                    continue;
                }

                var model = new ArmyListUnitModel
                {
                    Name = modelName,
                    Quantity = minQuantity,
                    MinQuantity = minQuantity,
                    MaxQuantity = Math.Max(minQuantity, maxQuantity),
                    M = modelProfile.M,
                    T = modelProfile.T,
                    Sv = modelProfile.Sv,
                    InvSv = modelProfile.InvSv,
                    W = modelProfile.W,
                    Ld = modelProfile.Ld,
                    OC = modelProfile.OC,
                    BaseSize = modelProfile.BaseSize,
                    Weapons = DatasheetParsingHelper.ParseLoadoutForModel(detail.Loadout, modelName, detail.Wargear, minQuantity)
                        .Select(parsedWeapon =>
                        {
                            var weapon = CreateArmyListWeaponFromWargear(parsedWeapon.Wargear);
                            weapon.Quantity = parsedWeapon.Quantity;
                            return weapon;
                        })
                        .ToList()
                };

                models.Add(model);
            }

            return models;
        }

        private List<string> CreateSelectedCompositionDescriptions(DatasheetDetail detail, string? selectedCostDescription = null)
        {
            var compositions = DatasheetParsingHelper.ParseUnitCompositionOptions(detail.UnitComposition);
            var selectedComposition = DatasheetParsingHelper.SelectCompositionOption(compositions, selectedCostDescription);
            if (selectedComposition.Count == 0)
            {
                return DatasheetParsingHelper.GetRawCompositionDescriptions(detail.UnitComposition);
            }

            return new List<string> { DatasheetParsingHelper.FormatComposition(selectedComposition) };
        }

        private List<string> CreateCompositionDescriptionsFromModels(IEnumerable<ArmyListUnitModel> models)
        {
            var entries = models.Select(model => new DatasheetCompositionEntry(model.Name, model.Quantity, model.Quantity)).ToList();
            return entries.Count == 0 ? new List<string>() : new List<string> { DatasheetParsingHelper.FormatComposition(entries) };
        }

        private DatasheetModel? FindModelProfile(DatasheetDetail detail, string modelName)
        {
            var normalizedModelName = modelName.Replace(" ", string.Empty);

            return detail.Models.FirstOrDefault(model =>
                       model.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase))
                   ?? detail.Models.FirstOrDefault(model =>
                       model.Name.Replace(" ", string.Empty).Contains(normalizedModelName, StringComparison.OrdinalIgnoreCase) ||
                       normalizedModelName.Contains(model.Name.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase))
                   ?? detail.Models.FirstOrDefault(model =>
                       model.Name.Equals(detail.Name, StringComparison.OrdinalIgnoreCase))
                   ?? detail.Models.FirstOrDefault();
        }

        private List<List<UnitCompositionEntry>> ParseUnitCompositionOptions(IEnumerable<DatasheetUnitComposition> composition)
        {
            var options = new List<List<UnitCompositionEntry>>();
            var currentOption = new List<UnitCompositionEntry>();

            foreach (var entry in composition)
            {
                var description = HtmlUtility.ConvertHtmlToPlainText(entry.Description).Trim();
                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                if (description.Equals("OR", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentOption.Count > 0)
                    {
                        options.Add(currentOption);
                        currentOption = new List<UnitCompositionEntry>();
                    }

                    continue;
                }

                currentOption.AddRange(ParseCompositionEntries(description));
            }

            if (currentOption.Count > 0)
            {
                options.Add(currentOption);
            }

            return options;
        }

        private List<ArmyListUnitWeapon> ParseLoadoutForModel(string loadoutText, string modelName, List<DatasheetWargear> allWargear, int modelQuantity)
        {
            var weapons = new List<ArmyListUnitWeapon>();
            if (string.IsNullOrWhiteSpace(loadoutText))
            {
                return weapons;
            }

            var cleanedLoadout = HtmlUtility.StripHtml(loadoutText);
            var loadoutLines = cleanedLoadout.Split(new[] { '.', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            foreach (var line in loadoutLines)
            {
                if (!TryGetLoadoutWeaponsForModel(line, modelName, out var weaponNames))
                {
                    continue;
                }

                foreach (var weaponName in weaponNames)
                {
                    var quantity = 1;
                    var cleanedWeaponName = weaponName;
                    var quantityMatch = Regex.Match(weaponName, @"^(\d+)\s+(.+)$");
                    if (quantityMatch.Success)
                    {
                        quantity = int.Parse(quantityMatch.Groups[1].Value);
                        cleanedWeaponName = quantityMatch.Groups[2].Value.Trim();
                    }

                    var wargear = FindWargear(allWargear, cleanedWeaponName);
                    if (wargear == null)
                    {
                        continue;
                    }

                    var weapon = CreateArmyListWeaponFromWargear(wargear);
                    weapon.Quantity = Math.Max(1, quantity * modelQuantity);
                    weapons.Add(weapon);
                }
            }

            return weapons;
        }

        private bool TryGetLoadoutWeaponsForModel(string loadoutLine, string modelName, out List<string> weaponNames)
        {
            weaponNames = new List<string>();
            var line = loadoutLine.Trim();
            var appliesToModel = false;
            var weaponsPart = string.Empty;

            var theModelMatch = Regex.Match(line, @"^The (.+?) is equipped with:(.+)$", RegexOptions.IgnoreCase);
            if (theModelMatch.Success)
            {
                weaponsPart = theModelMatch.Groups[2].Value.Trim();
                var targetModel = theModelMatch.Groups[1].Value.Trim();
                appliesToModel = targetModel.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                                 modelName.Contains(targetModel, StringComparison.OrdinalIgnoreCase);
            }

            var everyModelMatch = Regex.Match(line, @"^Every (.+?) is equipped with:(.+)$", RegexOptions.IgnoreCase);
            if (everyModelMatch.Success)
            {
                weaponsPart = everyModelMatch.Groups[2].Value.Trim();
                var targetModel = everyModelMatch.Groups[1].Value.Trim();
                appliesToModel = targetModel.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                                 targetModel.TrimEnd('s').Equals(modelName.TrimEnd('s'), StringComparison.OrdinalIgnoreCase) ||
                                 modelName.Contains(targetModel, StringComparison.OrdinalIgnoreCase) ||
                                 targetModel.Contains(modelName, StringComparison.OrdinalIgnoreCase);
            }

            var genericModelMatch = Regex.Match(line, @"^(?:This model|Every model) is equipped with:(.+)$", RegexOptions.IgnoreCase);
            if (genericModelMatch.Success)
            {
                weaponsPart = genericModelMatch.Groups[1].Value.Trim();
                appliesToModel = true;
            }

            if (!appliesToModel || string.IsNullOrWhiteSpace(weaponsPart))
            {
                return false;
            }

            weaponNames = weaponsPart.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            return weaponNames.Count > 0;
        }

        private DatasheetWargear? FindWargear(IEnumerable<DatasheetWargear> allWargear, string weaponName)
        {
            var singularWeaponName = weaponName.EndsWith('s') ? weaponName[..^1] : null;

            return allWargear.FirstOrDefault(wargear =>
                       wargear.Name.Equals(weaponName, StringComparison.OrdinalIgnoreCase) ||
                       weaponName.Contains(wargear.Name, StringComparison.OrdinalIgnoreCase))
                   ?? (singularWeaponName != null
                       ? allWargear.FirstOrDefault(wargear =>
                           wargear.Name.Equals(singularWeaponName, StringComparison.OrdinalIgnoreCase) ||
                           singularWeaponName.Contains(wargear.Name, StringComparison.OrdinalIgnoreCase))
                       : null);
        }

        private List<UnitCompositionEntry> SelectCompositionOption(List<List<UnitCompositionEntry>> options, string? selectedCostDescription)
        {
            if (options.Count == 0)
            {
                return new List<UnitCompositionEntry>();
            }

            if (!string.IsNullOrWhiteSpace(selectedCostDescription))
            {
                var costEntries = ParseCostDescriptionEntries(selectedCostDescription);
                if (costEntries.Count > 0)
                {
                    var exactMatch = options.FirstOrDefault(option => MatchesCompositionOption(option, costEntries));
                    if (exactMatch != null)
                    {
                        return exactMatch;
                    }

                    var targetTotal = costEntries.Sum(entry => entry.Quantity);
                    var countMatch = options.FirstOrDefault(option => option.Sum(entry => entry.Quantity) == targetTotal);
                    if (countMatch != null)
                    {
                        return countMatch;
                    }
                }
            }

            return options[0];
        }

        private List<UnitCompositionEntry> ParseCompositionEntries(string description)
        {
            var matches = Regex.Matches(description, @"(\d+)(?:-(\d+))?\s+(.+?)(?=(?:\s+and\s+\d)|$)", RegexOptions.IgnoreCase);
            var entries = new List<UnitCompositionEntry>();

            foreach (Match match in matches)
            {
                if (!match.Success)
                {
                    continue;
                }

                var minQuantity = int.Parse(match.Groups[1].Value);
                var maxQuantity = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : minQuantity;
                var name = match.Groups[3].Value.Trim();
                entries.Add(new UnitCompositionEntry(name, minQuantity, maxQuantity));
            }

            return entries;
        }

        private static bool MatchesCompositionOption(IEnumerable<UnitCompositionEntry> option, IEnumerable<CostDescriptionEntry> costEntries)
        {
            var optionList = option.ToList();
            var costList = costEntries.ToList();

            if (costList.Count == 1 && costList[0].IsGenericModelCount)
            {
                return optionList.Sum(entry => entry.Quantity) == costList[0].Quantity;
            }

            if (optionList.Count != costList.Count)
            {
                return false;
            }

            foreach (var costEntry in costList)
            {
                var matchingEntry = optionList.FirstOrDefault(entry => NamesMatch(entry.Name, costEntry.Name));
                if (matchingEntry == null || matchingEntry.Quantity != costEntry.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        private static string FormatComposition(IEnumerable<UnitCompositionEntry> entries)
            => string.Join(" and ", entries.Select(entry => $"{entry.Quantity} {entry.Name}"));

        private ArmyListUnitWeapon CreateArmyListWeaponFromWargear(DatasheetWargear wargear)
        {
            return new ArmyListUnitWeapon
            {
                Name = wargear.Name,
                Range = wargear.Range,
                Type = wargear.Type,
                A = wargear.A,
                BsWs = wargear.BsWs,
                S = wargear.S,
                AP = wargear.AP,
                D = wargear.D,
                Description = wargear.Description
            };
        }

        private ArmyListUnit? FindUnit(string unitId)
        {
            return EnsureActiveList().Units.FirstOrDefault(unit => unit.Id == unitId);
        }

        private ArmyListUnitModel? FindModel(string unitId, string modelId)
        {
            return FindUnit(unitId)?.Models.FirstOrDefault(model => model.Id == modelId);
        }

        private ArmyListUnitWeapon? FindWeapon(string unitId, string modelId, string weaponId)
        {
            return FindModel(unitId, modelId)?.Weapons.FirstOrDefault(weapon => weapon.Id == weaponId);
        }

        private List<ArmyListUnitModel> CloneModels(IEnumerable<ArmyListUnitModel> models)
        {
            return models.Select(model => new ArmyListUnitModel
            {
                Name = model.Name,
                Quantity = model.Quantity,
                MinQuantity = model.MinQuantity,
                MaxQuantity = model.MaxQuantity,
                M = model.M,
                T = model.T,
                Sv = model.Sv,
                InvSv = model.InvSv,
                W = model.W,
                Ld = model.Ld,
                OC = model.OC,
                BaseSize = model.BaseSize,
                Weapons = model.Weapons.Select(weapon => new ArmyListUnitWeapon
                {
                    Name = weapon.Name,
                    IsSelected = weapon.IsSelected,
                    Quantity = weapon.Quantity,
                    Range = weapon.Range,
                    Type = weapon.Type,
                    A = weapon.A,
                    BsWs = weapon.BsWs,
                    S = weapon.S,
                    AP = weapon.AP,
                    D = weapon.D,
                    Description = weapon.Description
                }).ToList()
            }).ToList();
        }

        private void ApplyAutomaticCostSelection(ArmyListUnit unit)
        {
            var matchingCost = FindMatchingCostOption(unit);
            if (matchingCost == null)
            {
                return;
            }

            unit.SelectedCostLine = matchingCost.Line;
            unit.SelectedCostDescription = matchingCost.Description;
            unit.SelectedPoints = matchingCost.Cost;
        }

        private ArmyListCostOption? FindMatchingCostOption(ArmyListUnit unit)
        {
            var totalModels = unit.Models.Sum(model => model.Quantity);
            if (totalModels <= 0 || unit.CostOptions.Count == 0)
            {
                return null;
            }

            var exactCompositionMatch = unit.CostOptions.FirstOrDefault(cost => MatchesExactComposition(cost.Description, unit.Models, totalModels));
            if (exactCompositionMatch != null)
            {
                return exactCompositionMatch;
            }

            var exactModelCountMatch = unit.CostOptions.FirstOrDefault(cost => MatchesModelCount(cost.Description, totalModels));
            if (exactModelCountMatch != null)
            {
                return exactModelCountMatch;
            }

            return unit.CostOptions.FirstOrDefault(cost => cost.Line == unit.SelectedCostLine)
                   ?? unit.CostOptions.FirstOrDefault();
        }

        private static bool MatchesExactComposition(string description, IEnumerable<ArmyListUnitModel> models, int totalModels)
        {
            var entries = ParseCostDescriptionEntries(description);
            if (entries.Count <= 1 || entries.Any(entry => entry.IsGenericModelCount))
            {
                return false;
            }

            if (entries.Sum(entry => entry.Quantity) != totalModels)
            {
                return false;
            }

            foreach (var entry in entries)
            {
                var matchingModel = models.FirstOrDefault(model => NamesMatch(model.Name, entry.Name));
                if (matchingModel == null || matchingModel.Quantity != entry.Quantity)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesModelCount(string description, int totalModels)
        {
            var entries = ParseCostDescriptionEntries(description);
            return entries.Count == 1 && entries[0].IsGenericModelCount && entries[0].Quantity == totalModels;
        }

        private static List<CostDescriptionEntry> ParseCostDescriptionEntries(string description)
        {
            return description.Split(new[] { " and ", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Select(ParseCostDescriptionEntry)
                .Where(entry => entry != null)
                .Cast<CostDescriptionEntry>()
                .ToList();
        }

        private static CostDescriptionEntry? ParseCostDescriptionEntry(string text)
        {
            var match = Regex.Match(text, @"^(\d+)\s+(.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var quantity = int.Parse(match.Groups[1].Value);
            var name = match.Groups[2].Value.Trim();
            var normalizedName = NormalizeName(name);

            return new CostDescriptionEntry(
                quantity,
                name,
                normalizedName is "model" or "models");
        }

        private static bool NamesMatch(string modelName, string costName)
        {
            var normalizedModel = NormalizeName(modelName);
            var normalizedCost = NormalizeName(costName);

            return normalizedModel == normalizedCost
                   || normalizedModel.Contains(normalizedCost, StringComparison.OrdinalIgnoreCase)
                   || normalizedCost.Contains(normalizedModel, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeName(string value)
        {
            var normalizedWords = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9\s]", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length > 3 && word.EndsWith('s') ? word[..^1] : word);

            return string.Join(' ', normalizedWords);
        }

        private sealed record CostDescriptionEntry(int Quantity, string Name, bool IsGenericModelCount);
        private sealed record UnitCompositionEntry(string Name, int MinQuantity, int MaxQuantity)
        {
            public int Quantity => MinQuantity;
        }

        private string GenerateUniqueListName(string baseName)
        {
            var existingNames = _collection.Lists
                .Select(list => list.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            var suffix = 2;
            while (existingNames.Contains($"{baseName} {suffix}"))
            {
                suffix++;
            }

            return $"{baseName} {suffix}";
        }
    }
}
