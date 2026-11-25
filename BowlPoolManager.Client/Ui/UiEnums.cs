namespace BowlPoolManager.Client.Ui;

/// <summary>How a <see cref="Card"/> looks.</summary>
public enum CardTone
{
    /// <summary>White card with a light shadow.</summary>
    Default,
    /// <summary>White card with a thin border and no shadow, for groups of cards.</summary>
    Outlined,
    /// <summary>Orange border: "this is you" or the one thing to look at.</summary>
    Highlight,
    /// <summary>Soft orange background: a hint or an explanation.</summary>
    Tint,
    /// <summary>Navy background with white text: a question that needs an answer.</summary>
    Dark
}

public enum StatusTone { Normal, Live, Alert }

public enum NoticeTone { Info, Tip, Success, Warning, Error }

public enum BadgeTone { Neutral, Live, Good, Alert }

public enum RowState
{
    Normal,
    /// <summary>The row is the player's own (orange bar on the left).</summary>
    Selected,
    /// <summary>The row was tapped and is waiting for a yes or no.</summary>
    Pending,
    Disabled
}

public enum CellKind
{
    Text,
    Rank,
    /// <summary>A name, cut with "..." when it does not fit; badges may follow it.</summary>
    Name,
    /// <summary>A big number at the right (points).</summary>
    Points,
    /// <summary>A secondary number.</summary>
    Muted
}

public enum PickButtonState
{
    Normal,
    /// <summary>The player's pick.</summary>
    Selected,
    /// <summary>Cannot be picked yet, for example the team is not decided.</summary>
    Disabled,
    /// <summary>Needs attention, for example a game is still missing a pick.</summary>
    Error,
    /// <summary>The game is final and the pick was right.</summary>
    Correct,
    /// <summary>The game is final and the pick was wrong.</summary>
    Wrong
}

public enum PoolSwitcherMode
{
    /// <summary>Tabs inside the navy header (phones). Placed once, in the layout.</summary>
    HeaderTabs,
    /// <summary>A segmented control inside the page (wide screens). Hidden on phones.</summary>
    Segmented
}
