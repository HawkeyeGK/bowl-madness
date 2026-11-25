using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BowlPoolManager.Client.Ui;

/// <summary>What <see cref="PrimaryButton"/> and <see cref="SecondaryButton"/> have in common.</summary>
public abstract class AppButtonBase : ComponentBase
{
    /// <summary>The words on the button. Always give it, even for an icon-only button (it is read aloud).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
    /// <summary>A MudBlazor icon shown before the words, for example <c>Icons.Material.Filled.Print</c>.</summary>
    [Parameter] public string? Icon { get; set; }
    /// <summary>Show only the icon (the words stay as the accessible name). Needs <see cref="Icon"/>.</summary>
    [Parameter] public bool IconOnly { get; set; }
    /// <summary>Makes the button a link.</summary>
    [Parameter] public string? Href { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool FullWidth { get; set; }
    /// <summary>56 px tall instead of 48 px: the one main action at the bottom of a screen.</summary>
    [Parameter] public bool Large { get; set; }
    /// <summary>Use <c>Submit</c> inside a form.</summary>
    [Parameter] public MudBlazor.ButtonType ButtonType { get; set; } = MudBlazor.ButtonType.Button;
    /// <summary>The accessible name when the words are not enough ("Print the bracket form").</summary>
    [Parameter] public string? AriaLabel { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = default!;

    /// <summary>
    /// Addresses under "/.auth/" (sign in, sign out) belong to Azure, not to this app. A plain link to one is routed
    /// inside the app and shows "Page not found", so such a button navigates with a full page load instead.
    /// </summary>
    protected bool LoadsOutsideApp => Href != null && Href.StartsWith("/.auth/", StringComparison.OrdinalIgnoreCase);

    protected string? LinkHref => LoadsOutsideApp ? null : Href;

    protected EventCallback<MouseEventArgs> ClickHandler => LoadsOutsideApp
        ? EventCallback.Factory.Create<MouseEventArgs>(this, () => Nav.NavigateTo(Href!, forceLoad: true))
        : OnClick;

    protected string CssFor(string kind) =>
        $"bm-btn bm-btn--{kind}" + (Large ? " bm-btn--large" : "") + (IconOnly ? " bm-btn--icononly" : "");
}
