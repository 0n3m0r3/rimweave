#if DEBUG
using Verse;

namespace RimWeave.Debugging;

// Only the menu is presentation. Its selected gameplay action runs after that scope has ended.
internal static class CommandSelection
{
    internal static void Show(IEnumerable<(string Label, Action Execute)> choices) =>
        InspectionPresentation.PreserveRandomState(() =>
            Find.WindowStack.Add(new SelectionMenu(choices)));

    internal static void Present(Action<Action<Action>> presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        Action? selected = null;
        InspectionPresentation.PreserveRandomState(() => presentation(action => selected = action));
        selected?.Invoke();
    }

    private sealed class SelectionMenu : FloatMenu
    {
        private Action<Action>? _select;

        internal SelectionMenu(IEnumerable<(string Label, Action Execute)> choices)
            : this(choices.Select(choice => new FloatMenuOption(choice.Label, choice.Execute)).ToList()) { }

        private SelectionMenu(List<FloatMenuOption> choices) : base(choices)
        {
            foreach (var option in choices)
            {
                var execute = option.action;
                option.action = () => _select?.Invoke(execute);
            }
        }

        public override void WindowOnGUI() => PresentFrame(base.WindowOnGUI);

        private void PresentFrame(Action draw) => Present(select =>
        {
            _select = select;
            try { draw(); }
            finally { _select = null; }
        });

        public override void Close(bool doCloseSound = true) =>
            InspectionPresentation.PreserveRandomState(() => base.Close(doCloseSound));
    }
}
#endif
