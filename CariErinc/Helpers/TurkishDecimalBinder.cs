using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Logging;

namespace CariErinc.Helpers;

/// <summary>
/// Hem virgüllü (123,45) hem noktalı (123.45) ondalık sayıları 
/// merkezi olarak çözen Model Binder.
/// </summary>
public class TurkishDecimalBinder : IModelBinder
{
    private readonly DecimalModelBinder _baseBinder;

    public TurkishDecimalBinder(ILoggerFactory loggerFactory)
    {
        _baseBinder = new DecimalModelBinder(NumberStyles.Any, loggerFactory);
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return _baseBinder.BindModelAsync(bindingContext);
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            return _baseBinder.BindModelAsync(bindingContext);
        }

        // Temizlik: Boşlukları ve para birimi simgelerini kaldır
        value = value.Trim().Replace("₺", "").Replace(" ", "");

        // Mantık: 
        // 1. tr-TR ile dene (Merkezi çözüm: 1.234,56 veya 1234,56)
        // 2. Başarısız olursa Invariant (noktalı) dene (1234.56)
        
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var trResult))
        {
            bindingContext.Result = ModelBindingResult.Success(trResult);
            return Task.CompletedTask;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var invResult))
        {
            bindingContext.Result = ModelBindingResult.Success(invResult);
            return Task.CompletedTask;
        }

        // İkisi de olmazsa hata fırlatmak yerine varsayılan binder'a bırak (belki hata mesajı üretir)
        return _baseBinder.BindModelAsync(bindingContext);
    }
}

public class TurkishDecimalBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
        {
            return new TurkishDecimalBinder(context.Services.GetRequiredService<ILoggerFactory>());
        }

        return null;
    }
}
