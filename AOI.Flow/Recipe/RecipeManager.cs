using AOI.Flow.Model;
using AOI.Flow.Node;
using System.Collections.Concurrent;

namespace AOI.Flow.Recipe;

/// <summary>
/// Recipe管理器 - 管理所有配方并提供运行时支持
/// </summary>
public class RecipeManager
{
    private readonly ConcurrentDictionary<string, Recipe> _recipes = new();
    private readonly RecipeFactory _factory;
    private readonly RecipeValidator _validator;
    private string? _defaultRecipeId;

    public RecipeManager(RecipeFactory? factory = null, RecipeValidator? validator = null)
    {
        _factory = factory ?? new RecipeFactory();
        _validator = validator ?? new RecipeValidator();
    }

    /// <summary>
    /// 获取或设置默认Recipe ID
    /// </summary>
    public string? DefaultRecipeId
    {
        get => _defaultRecipeId;
        set
        {
            if (value != null && !_recipes.ContainsKey(value))
                throw new ArgumentException($"Recipe '{value}' not found");
            _defaultRecipeId = value;
        }
    }

    /// <summary>
    /// 注册Recipe
    /// </summary>
    public void RegisterRecipe(Recipe recipe)
    {
        var validation = _validator.Validate(recipe);
        if (!validation.IsValid)
        {
            throw new RecipeValidationException(validation.Errors);
        }

        recipe.ModifiedAt = DateTime.UtcNow;
        _recipes[recipe.Id] = recipe;

        // 如果这是第一个Recipe，设为默认
        if (_defaultRecipeId == null && recipe.Status == RecipeStatus.Active)
        {
            _defaultRecipeId = recipe.Id;
        }
    }

    /// <summary>
    /// 批量注册Recipe
    /// </summary>
    public void RegisterRecipes(IEnumerable<Recipe> recipes)
    {
        foreach (var recipe in recipes)
        {
            RegisterRecipe(recipe);
        }
    }

    /// <summary>
    /// 获取Recipe
    /// </summary>
    public Recipe? GetRecipe(string recipeId)
    {
        return _recipes.GetValueOrDefault(recipeId);
    }

    /// <summary>
    /// 通过产品代码获取Recipe
    /// </summary>
    public Recipe? GetRecipeByProductCode(string productCode)
    {
        return _recipes.Values.FirstOrDefault(r =>
            r.ProductCode.Equals(productCode, StringComparison.OrdinalIgnoreCase) &&
            r.Status == RecipeStatus.Active);
    }

    /// <summary>
    /// 获取默认Recipe
    /// </summary>
    public Recipe? GetDefaultRecipe()
    {
        if (_defaultRecipeId != null)
            return _recipes.GetValueOrDefault(_defaultRecipeId);

        return _recipes.Values.FirstOrDefault(r => r.Status == RecipeStatus.Active);
    }

    /// <summary>
    /// 获取所有Recipe
    /// </summary>
    public IReadOnlyCollection<Recipe> GetAllRecipes()
    {
        return _recipes.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取指定状态的Recipe
    /// </summary>
    public IEnumerable<Recipe> GetRecipesByStatus(RecipeStatus status)
    {
        return _recipes.Values.Where(r => r.Status == status);
    }

    /// <summary>
    /// 移除Recipe
    /// </summary>
    public bool RemoveRecipe(string recipeId)
    {
        if (_defaultRecipeId == recipeId)
        {
            _defaultRecipeId = null;
        }
        return _recipes.TryRemove(recipeId, out _);
    }

    /// <summary>
    /// 激活Recipe
    /// </summary>
    public bool ActivateRecipe(string recipeId)
    {
        if (_recipes.TryGetValue(recipeId, out var recipe))
        {
            recipe.Status = RecipeStatus.Active;
            recipe.ModifiedAt = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 归档Recipe
    /// </summary>
    public bool ArchiveRecipe(string recipeId)
    {
        if (_recipes.TryGetValue(recipeId, out var recipe))
        {
            recipe.Status = RecipeStatus.Archived;
            recipe.ModifiedAt = DateTime.UtcNow;
            if (_defaultRecipeId == recipeId)
            {
                _defaultRecipeId = null;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// 从Recipe创建Flow定义
    /// </summary>
    public FlowDefinition CreateFlowDefinitionFromRecipe(Recipe recipe)
    {
        return _factory.CreateFlowDefinition(recipe);
    }

    /// <summary>
    /// 克隆Recipe
    /// </summary>
    public Recipe CloneRecipe(string sourceRecipeId, string newName, string? newProductCode = null)
    {
        if (!_recipes.TryGetValue(sourceRecipeId, out var source))
            throw new ArgumentException($"Source recipe '{sourceRecipeId}' not found");

        var clone = new Recipe
        {
            Id = Guid.NewGuid().ToString(),
            Name = newName,
            ProductCode = newProductCode ?? source.ProductCode,
            Description = $"Cloned from {source.Name}",
            CustomerCode = source.CustomerCode,
            Version = "1.0.0",
            Status = RecipeStatus.Draft,
            FlowDefinitionName = source.FlowDefinitionName,
            NodeParameters = source.NodeParameters.ToDictionary(
                kvp => kvp.Key,
                kvp => CloneNodeParameters(kvp.Value)),
            GlobalParameters = source.GlobalParameters.ToDictionary(
                kvp => kvp.Key,
                kvp => CloneParameterValue(kvp.Value)),
            InspectionSpecs = CloneInspectionSpecs(source.InspectionSpecs),
            DeviceConfigs = CloneDeviceConfigs(source.DeviceConfigs),
            Dimensions = CloneDimensions(source.Dimensions),
            Extensions = new Dictionary<string, object>(source.Extensions),
            ParentRecipeId = source.Id,
            Tags = source.Tags.ToList()
        };

        return clone;
    }

    /// <summary>
    /// 获取节点的运行时参数（合并全局参数和节点参数）
    /// </summary>
    public Dictionary<string, ParameterValue> GetNodeRuntimeParameters(string recipeId, string nodeId)
    {
        var recipe = GetRecipe(recipeId);
        if (recipe == null)
            return new Dictionary<string, ParameterValue>();

        var result = new Dictionary<string, ParameterValue>(recipe.GlobalParameters);

        if (recipe.NodeParameters.TryGetValue(nodeId, out var nodeParams))
        {
            foreach (var kvp in nodeParams.Config)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    #region Clone Helpers

    private static NodeParameters CloneNodeParameters(NodeParameters source)
    {
        return new NodeParameters
        {
            NodeType = source.NodeType,
            IsEnabled = source.IsEnabled,
            Config = source.Config.ToDictionary(kvp => kvp.Key, kvp => CloneParameterValue(kvp.Value)),
            TimeoutMs = source.TimeoutMs,
            RetryCount = source.RetryCount,
            RetryIntervalMs = source.RetryIntervalMs
        };
    }

    private static ParameterValue CloneParameterValue(ParameterValue source)
    {
        return new ParameterValue
        {
            Type = source.Type,
            Value = source.Value, // 假设值是可复制的
            Unit = source.Unit,
            Description = source.Description,
            Range = source.Range == null ? null : new ParameterRange
            {
                Min = source.Range.Min,
                Max = source.Range.Max,
                Step = source.Range.Step,
                AllowedValues = source.Range.AllowedValues?.ToList()
            }
        };
    }

    private static InspectionSpecs CloneInspectionSpecs(InspectionSpecs source)
    {
        return new InspectionSpecs
        {
            Items = source.Items.Select(item => new InspectionItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = item.Name,
                Type = item.Type,
                IsEnabled = item.IsEnabled,
                MinValue = item.MinValue,
                MaxValue = item.MaxValue,
                NominalValue = item.NominalValue,
                Tolerance = item.Tolerance,
                Unit = item.Unit,
                Severity = item.Severity,
                ROI = item.ROI == null ? null : new RegionOfInterest
                {
                    X = item.ROI.X,
                    Y = item.ROI.Y,
                    Width = item.ROI.Width,
                    Height = item.ROI.Height,
                    Rotation = item.ROI.Rotation,
                    Name = item.ROI.Name
                },
                AlgorithmParams = item.AlgorithmParams.ToDictionary(
                    kvp => kvp.Key,
                    kvp => CloneParameterValue(kvp.Value))
            }).ToList(),
            Criteria = new PassCriteria
            {
                MaxDefectCount = source.Criteria.MaxDefectCount,
                MaxCriticalDefects = source.Criteria.MaxCriticalDefects,
                RequireCriticalItemsPass = source.Criteria.RequireCriticalItemsPass
            },
            ImageQuality = new ImageQualityRequirements
            {
                MinResolutionX = source.ImageQuality.MinResolutionX,
                MinResolutionY = source.ImageQuality.MinResolutionY,
                MinContrast = source.ImageQuality.MinContrast,
                MaxNoise = source.ImageQuality.MaxNoise,
                MinSharpness = source.ImageQuality.MinSharpness
            }
        };
    }

    private static DeviceConfigs CloneDeviceConfigs(DeviceConfigs source)
    {
        return new DeviceConfigs
        {
            Cameras = source.Cameras.ToDictionary(
                kvp => kvp.Key,
                kvp => new CameraConfig
                {
                    DeviceId = kvp.Value.DeviceId,
                    ExposureTime = kvp.Value.ExposureTime,
                    Gain = kvp.Value.Gain,
                    ROI_X = kvp.Value.ROI_X,
                    ROI_Y = kvp.Value.ROI_Y,
                    ROI_Width = kvp.Value.ROI_Width,
                    ROI_Height = kvp.Value.ROI_Height,
                    BinningX = kvp.Value.BinningX,
                    BinningY = kvp.Value.BinningY,
                    PixelFormat = kvp.Value.PixelFormat,
                    FrameRate = kvp.Value.FrameRate,
                    CustomParams = kvp.Value.CustomParams.ToDictionary(
                        cp => cp.Key,
                        cp => CloneParameterValue(cp.Value))
                }),
            Lights = source.Lights.ToDictionary(
                kvp => kvp.Key,
                kvp => new LightConfig
                {
                    DeviceId = kvp.Value.DeviceId,
                    Intensity = kvp.Value.Intensity,
                    DurationMs = kvp.Value.DurationMs,
                    Color = kvp.Value.Color,
                    Mode = kvp.Value.Mode,
                    StrobeDelayUs = kvp.Value.StrobeDelayUs
                }),
            Axes = source.Axes.ToDictionary(
                kvp => kvp.Key,
                kvp => new AxisConfig
                {
                    DeviceId = kvp.Value.DeviceId,
                    Speed = kvp.Value.Speed,
                    Acceleration = kvp.Value.Acceleration,
                    Deceleration = kvp.Value.Deceleration,
                    HomePosition = kvp.Value.HomePosition,
                    Positions = kvp.Value.Positions.Select(p => new PositionPreset
                    {
                        Name = p.Name,
                        Position = p.Position,
                        Speed = p.Speed
                    }).ToList()
                }),
            IOs = source.IOs.ToDictionary(
                kvp => kvp.Key,
                kvp => new IOConfig
                {
                    DeviceId = kvp.Value.DeviceId,
                    InitialStates = new Dictionary<string, bool>(kvp.Value.InitialStates),
                    PulseDurations = new Dictionary<string, int>(kvp.Value.PulseDurations)
                })
        };
    }

    private static ProductDimensions CloneDimensions(ProductDimensions source)
    {
        return new ProductDimensions
        {
            Width = source.Width,
            Height = source.Height,
            Thickness = source.Thickness,
            Weight = source.Weight,
            Unit = source.Unit,
            CustomDimensions = new Dictionary<string, double>(source.CustomDimensions)
        };
    }

    #endregion
}

/// <summary>
/// Recipe工厂 - 从Recipe创建Flow定义
/// </summary>
public class RecipeFactory
{
    public virtual FlowDefinition CreateFlowDefinition(Recipe recipe)
    {
        var definition = new FlowDefinition
        {
            Name = recipe.FlowDefinitionName
        };

        // TODO: 根据Recipe的FlowDefinitionName加载基础Flow模板
        // 然后应用Recipe中的参数覆盖默认配置

        // 这里简化处理，实际应根据FlowDefinitionName从配置库加载
        // 然后结合Recipe参数创建具体Flow

        return definition;
    }
}

/// <summary>
/// Recipe验证器
/// </summary>
public class RecipeValidator
{
    public ValidationResult Validate(Recipe recipe)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(recipe.Id))
            errors.Add("Recipe ID is required");

        if (string.IsNullOrWhiteSpace(recipe.Name))
            errors.Add("Recipe name is required");

        if (string.IsNullOrWhiteSpace(recipe.ProductCode))
            errors.Add("Product code is required");

        if (string.IsNullOrWhiteSpace(recipe.FlowDefinitionName))
            errors.Add("Flow definition name is required");

        // 验证检测项
        foreach (var item in recipe.InspectionSpecs.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                errors.Add($"Inspection item name is required");

            if (item.MinValue > item.MaxValue)
                errors.Add($"Inspection item '{item.Name}': Min value cannot be greater than max value");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class RecipeValidationException : Exception
{
    public List<string> ValidationErrors { get; }

    public RecipeValidationException(List<string> errors)
        : base("Recipe validation failed: " + string.Join("; ", errors))
    {
        ValidationErrors = errors;
    }
}
