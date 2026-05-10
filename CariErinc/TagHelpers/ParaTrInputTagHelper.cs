using CariErinc.Formatting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace CariErinc.TagHelpers;

/// <summary>
/// Para alanı: tr-TR metin (1.234,56), model bağlama ve data-val ile uyumlu.
/// Kullanım: &lt;input para-tr-for="BirimFiyat" class="form-control" /&gt;
/// </summary>
[HtmlTargetElement("input", Attributes = "para-tr-for", TagStructure = TagStructure.WithoutEndTag)]
public class ParaTrInputTagHelper : TagHelper
{
    private readonly IHtmlGenerator _generator;

    public ParaTrInputTagHelper(IHtmlGenerator generator) => _generator = generator;

    [HtmlAttributeName("para-tr-for")]
    public ModelExpression ParaTrFor { get; set; } = null!;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var modelExplorer = ParaTrFor.ModelExplorer;
        decimal d = modelExplorer?.Model switch
        {
            decimal x => x,
            null => 0m,
            _ => Convert.ToDecimal(modelExplorer.Model)
        };

        var valueText = TurkishPara.Format(d);

        var tb = _generator.GenerateTextBox(
            ViewContext,
            modelExplorer!,
            ParaTrFor.Name,
            valueText,
            format: null,
            htmlAttributes: new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["class"] = MergeClass(context.AllAttributes["class"]?.Value.ToString(), "input-para-tr"),
                ["inputmode"] = "decimal",
                ["autocomplete"] = "off"
            });

        if (context.AllAttributes.ContainsName("readonly"))
            tb.Attributes["readonly"] = "readonly";

        output.TagName = null;
        output.Content.SetHtmlContent(tb);
    }

    private static string MergeClass(string? existing, string extra)
    {
        if (string.IsNullOrWhiteSpace(existing)) return extra;
        return existing.Contains("input-para-tr", StringComparison.Ordinal) ? existing : $"{existing} {extra}";
    }
}
