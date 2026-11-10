using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SantasLittleHelpers.Savegames;
using SantasLittleHelpers.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace SantasLittleHelpers.Home
{
    /// <summary>
    /// Programmatic UI Toolkit view. All controls are ordinary focusable elements,
    /// so keyboard tab navigation and the visible focus ring work in windowed and
    /// fullscreen resolutions without a prefab dependency.
    /// </summary>
    public sealed class HomeMenuView : IDisposable
    {
        private readonly VisualElement root;
        private readonly StyleSheet styleSheet;
        private HomeMenuModel model;
        private bool disposed;
        private bool initialFocusApplied;

        private Label statusLabel;
        private Label selectionLabel;
        private Label emptyLabel;
        private Label deleteMessage;
        private Label volumeValue;
        private Label sensitivityValue;
        private Button fullscreenValue;
        private Label settingsSummary;
        private VisualElement slotList;
        private VisualElement settingsPanel;
        private VisualElement deleteOverlay;
        private Button createButton;
        private Button renameButton;
        private Button loadButton;
        private Button deleteButton;
        private Button soloButton;
        private Button hostButton;
        private Button joinButton;
        private TextField newNameField;
        private TextField renameField;
        private TextField endpointField;
        private TextField portField;

        public HomeMenuView(VisualElement root, StyleSheet styleSheet)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.styleSheet = styleSheet;
            Build();
        }

        public void SetModel(HomeMenuModel nextModel)
        {
            if (model != null)
            {
                model.Changed -= Refresh;
            }

            model = nextModel;
            if (model != null)
            {
                model.Changed += Refresh;
            }

            Refresh();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (model != null)
            {
                model.Changed -= Refresh;
            }

            root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void Build()
        {
            root.Clear();
            root.AddToClassList("home-root");
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            AddBackdrop();

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("home-scroll");
            root.Add(scroll);

            VisualElement shell = new VisualElement();
            shell.AddToClassList("home-shell");
            scroll.Add(shell);

            VisualElement header = new VisualElement();
            header.AddToClassList("home-header");
            shell.Add(header);

            Label kicker = new Label("WERKSTATT AM NORDPOL");
            kicker.AddToClassList("home-kicker");
            header.Add(kicker);

            Label title = new Label("Santa's Little Helpers");
            title.AddToClassList("home-title");
            header.Add(title);

            Label subtitle = new Label(
                "Wähle einen Spielstand für Solo oder Host – oder besuche eine befreundete Hütte.");
            subtitle.AddToClassList("home-subtitle");
            header.Add(subtitle);

            statusLabel = new Label();
            statusLabel.AddToClassList("home-status");
            statusLabel.AddToClassList("status-info");
            statusLabel.enableRichText = false;
            shell.Add(statusLabel);

            VisualElement content = new VisualElement();
            content.AddToClassList("home-content");
            shell.Add(content);

            content.Add(BuildSlotsCard());
            content.Add(BuildStartCard());

            VisualElement settingsToggleRow = new VisualElement();
            settingsToggleRow.AddToClassList("home-settings-toggle-row");
            shell.Add(settingsToggleRow);

            Button settingsToggle = CreateButton("Einstellungen", "secondary", () =>
            {
                if (model == null)
                {
                    return;
                }

                if (model.IsSettingsOpen)
                {
                    model.CloseSettings();
                }
                else
                {
                    model.OpenSettings();
                }
            });
            settingsToggle.AddToClassList("home-settings-toggle");
            settingsToggleRow.Add(settingsToggle);
            settingsToggleRow.Add(new Label("Lautstärke, Maussensitivität und Fenstermodus als Entwurf prüfen."));
            settingsToggleRow[1].AddToClassList("home-muted");

            settingsPanel = BuildSettingsCard();
            shell.Add(settingsPanel);

            deleteOverlay = BuildDeleteOverlay();
            root.Add(deleteOverlay);

            Refresh();
        }

        private void AddBackdrop()
        {
            VisualElement backdrop = new VisualElement();
            backdrop.AddToClassList("home-backdrop");
            root.Add(backdrop);

            VisualElement glow = new VisualElement();
            glow.AddToClassList("home-glow");
            backdrop.Add(glow);

            for (int index = 0; index < 10; index++)
            {
                VisualElement dot = new VisualElement();
                dot.AddToClassList("home-snow-dot");
                dot.AddToClassList("snow-" + index);
                backdrop.Add(dot);
            }
        }

        private VisualElement BuildSlotsCard()
        {
            VisualElement card = CreateCard();
            card.AddToClassList("home-card-slots");

            VisualElement header = new VisualElement();
            header.AddToClassList("home-card-header");
            card.Add(header);

            Label title = new Label("Spielstände");
            title.AddToClassList("home-card-title");
            header.Add(title);

            Button refresh = CreateButton("Aktualisieren", "ghost", () =>
                RunAsync(model?.RefreshSlotsAsync()));
            refresh.AddToClassList("home-compact");
            header.Add(refresh);

            emptyLabel = new Label();
            emptyLabel.AddToClassList("home-empty");
            card.Add(emptyLabel);

            ScrollView listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.AddToClassList("home-slot-scroll");
            slotList = new VisualElement();
            slotList.AddToClassList("home-slot-list");
            listScroll.Add(slotList);
            card.Add(listScroll);

            selectionLabel = new Label();
            selectionLabel.AddToClassList("home-selection");
            card.Add(selectionLabel);

            VisualElement newRow = new VisualElement();
            newRow.AddToClassList("home-field-row");
            card.Add(newRow);

            newNameField = CreateTextField("Neuer Spielstand", 64);
            newNameField.RegisterValueChangedCallback(evt => model?.SetNewSlotName(evt.newValue));
            newRow.Add(newNameField);

            createButton = CreateButton("Anlegen", "primary", () =>
                RunAsync(model?.CreateSlotAsync(model.NewSlotName)));
            newRow.Add(createButton);

            VisualElement renameRow = new VisualElement();
            renameRow.AddToClassList("home-field-row");
            card.Add(renameRow);

            renameField = CreateTextField("Ausgewählten umbenennen", 64);
            renameField.RegisterValueChangedCallback(evt => model?.SetRenameSlotName(evt.newValue));
            renameRow.Add(renameField);

            renameButton = CreateButton("Umbenennen", "secondary", () =>
                RunAsync(model?.RenameSelectedAsync(model.RenameSlotName)));
            renameRow.Add(renameButton);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("home-button-row");
            card.Add(actions);

            loadButton = CreateButton("Laden", "secondary", () =>
                RunAsync(model?.LoadSelectedAsync()));
            actions.Add(loadButton);

            deleteButton = CreateButton("Löschen", "danger", () => model?.BeginDeleteConfirmation());
            actions.Add(deleteButton);

            return card;
        }

        private VisualElement BuildStartCard()
        {
            VisualElement card = CreateCard();
            card.AddToClassList("home-card-start");

            Label title = new Label("Spiel starten");
            title.AddToClassList("home-card-title");
            card.Add(title);

            Label soloHint = new Label(
                "Solo und Host benötigen einen geladenen Host-Spielstand. Join tritt ohne lokalen Slot bei.");
            soloHint.AddToClassList("home-copy");
            card.Add(soloHint);

            soloButton = CreateButton("Solo starten", "primary home-launch", () =>
                RunAsync(model?.LaunchSoloAsync()));
            card.Add(soloButton);

            hostButton = CreateButton("Host starten · Lobby öffnen", "secondary home-launch", () =>
                RunAsync(model?.LaunchHostAsync()));
            card.Add(hostButton);

            VisualElement divider = new VisualElement();
            divider.AddToClassList("home-divider");
            card.Add(divider);

            Label joinTitle = new Label("Freundlicher Besuch");
            joinTitle.AddToClassList("home-card-subtitle");
            card.Add(joinTitle);

            Label joinHint = new Label("IPv4-Adresse und UDP-Port der Hütte. Kein Spielstand nötig.");
            joinHint.AddToClassList("home-copy");
            card.Add(joinHint);

            VisualElement joinRow = new VisualElement();
            joinRow.AddToClassList("home-field-row");
            card.Add(joinRow);

            endpointField = CreateTextField("IPv4-Adresse", 15);
            endpointField.RegisterValueChangedCallback(evt => model?.SetEndpointText(evt.newValue));
            joinRow.Add(endpointField);

            portField = CreateTextField("UDP-Port", 5);
            portField.RegisterValueChangedCallback(evt => model?.SetPortText(evt.newValue));
            portField.AddToClassList("home-port-field");
            joinRow.Add(portField);

            joinButton = CreateButton("Beitreten", "secondary home-join", () =>
                RunAsync(model?.LaunchJoinAsync()));
            card.Add(joinButton);

            Label footer = new Label("Sitzung, Szenen und Fortschritt übernimmt der Integrationsdienst.");
            footer.AddToClassList("home-footnote");
            card.Add(footer);

            return card;
        }

        private VisualElement BuildSettingsCard()
        {
            VisualElement card = CreateCard();
            card.AddToClassList("home-settings-card");

            VisualElement header = new VisualElement();
            header.AddToClassList("home-card-header");
            card.Add(header);

            Label title = new Label("Einstellungen");
            title.AddToClassList("home-card-title");
            header.Add(title);

            Button close = CreateButton("Schließen", "ghost", () => model?.CloseSettings());
            close.AddToClassList("home-compact");
            header.Add(close);

            settingsSummary = new Label();
            settingsSummary.AddToClassList("home-copy");
            card.Add(settingsSummary);

            card.Add(BuildStepper(
                "Lautstärke",
                () => model?.PreviewMasterVolume(model.SettingsDraft.MasterVolume - 0.05f),
                () => model?.PreviewMasterVolume(model.SettingsDraft.MasterVolume + 0.05f),
                out volumeValue));

            card.Add(BuildStepper(
                "Maussensitivität",
                () => model?.PreviewMouseSensitivity(model.SettingsDraft.MouseSensitivity - 0.01f),
                () => model?.PreviewMouseSensitivity(model.SettingsDraft.MouseSensitivity + 0.01f),
                out sensitivityValue));

            VisualElement screenRow = new VisualElement();
            screenRow.AddToClassList("home-step-row");
            Label screenLabel = new Label("Fenstermodus");
            screenLabel.AddToClassList("home-step-label");
            screenRow.Add(screenLabel);

            Button screenButton = CreateButton("Vollbild", "secondary", () =>
            {
                if (model != null)
                {
                    model.PreviewFullscreen(!model.SettingsDraft.Fullscreen);
                }
            });
            screenButton.AddToClassList("home-step-button-wide");
            screenRow.Add(screenButton);
            fullscreenValue = screenButton;
            card.Add(screenRow);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("home-button-row");
            card.Add(actions);

            actions.Add(CreateButton("Übernehmen", "primary", () =>
                RunAsync(model?.ApplySettingsAsync())));
            actions.Add(CreateButton("Verwerfen", "secondary", () => model?.CancelSettings()));
            actions.Add(CreateButton("Standardwerte", "ghost", () => model?.DefaultsSettings()));

            return card;
        }

        private VisualElement BuildStepper(string label, Action decrement, Action increment, out Label valueLabel)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("home-step-row");

            Label name = new Label(label);
            name.AddToClassList("home-step-label");
            row.Add(name);

            row.Add(CreateButton("−", "step", decrement));
            valueLabel = new Label();
            valueLabel.AddToClassList("home-step-value");
            row.Add(valueLabel);
            row.Add(CreateButton("+", "step", increment));
            return row;
        }

        private VisualElement BuildDeleteOverlay()
        {
            VisualElement overlay = new VisualElement();
            overlay.AddToClassList("home-overlay");
            overlay.style.display = DisplayStyle.None;

            VisualElement card = new VisualElement();
            card.AddToClassList("home-dialog");
            overlay.Add(card);

            Label title = new Label("Spielstand löschen?");
            title.AddToClassList("home-card-title");
            card.Add(title);

            deleteMessage = new Label();
            deleteMessage.AddToClassList("home-copy");
            card.Add(deleteMessage);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("home-button-row");
            card.Add(actions);

            actions.Add(CreateButton("Endgültig löschen", "danger", () =>
                RunAsync(model?.ConfirmDeleteAsync())));
            actions.Add(CreateButton("Abbrechen", "secondary", () => model?.CancelDeleteConfirmation()));

            return overlay;
        }

        private static VisualElement CreateCard()
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("home-card");
            return card;
        }

        private static Button CreateButton(string text, string className, Action clicked)
        {
            Button button = new Button(clicked) { text = text };
            button.AddToClassList("home-button");
            if (!string.IsNullOrWhiteSpace(className))
            {
                foreach (string part in className.Split(' '))
                {
                    button.AddToClassList("home-button-" + part);
                }
            }

            button.focusable = true;
            return button;
        }

        private static TextField CreateTextField(string placeholder, int maxLength)
        {
            TextField field = new TextField();
            field.AddToClassList("home-field");
            field.maxLength = maxLength;
            field.isDelayed = false;
            if (!string.IsNullOrWhiteSpace(placeholder))
            {
                field.textEdition.placeholder = placeholder;
            }

            field.focusable = true;
            return field;
        }

        private void Refresh()
        {
            if (model == null || disposed)
            {
                return;
            }

            SetStatus(model.StatusKind, model.StatusMessage);

            emptyLabel.text = "Noch keine Spielstände. Lege deinen ersten gemütlichen Spielstand an.";
            emptyLabel.style.display = model.Slots.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            RebuildSlotList();

            SaveSlotSummary selected = model.SelectedSummary;
            if (selected == null)
            {
                selectionLabel.text = model.Slots.Count == 0
                    ? "Kein Spielstand vorhanden."
                    : "Kein Spielstand ausgewählt.";
                selectionLabel.RemoveFromClassList("selection-ok");
                selectionLabel.AddToClassList("selection-muted");
            }
            else
            {
                string detail = HomeMenuModel.DescribeSlotStatus(selected);
                if (selected.IsLoadable)
                {
                    detail += $" · {selected.CompletedMissionCount} Mission(en)";
                    if (selected.CreatedAtUtc.HasValue)
                    {
                        detail += " · " + selected.CreatedAtUtc.Value.ToLocalTime().ToString("dd.MM.yyyy");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(selected.ErrorMessage))
                {
                    detail += " · " + selected.ErrorMessage;
                }

                selectionLabel.text = $"Ausgewählt: {selected.DisplayName} · {detail}";
                selectionLabel.RemoveFromClassList("selection-muted");
                selectionLabel.AddToClassList(selected.IsLoadable ? "selection-ok" : "selection-error");
            }

            SyncField(newNameField, model.NewSlotName);
            SyncField(renameField, model.RenameSlotName);
            SyncField(endpointField, string.IsNullOrEmpty(model.EndpointText)
                ? HomeMenuModel.DefaultEndpoint
                : model.EndpointText);
            SyncField(portField, string.IsNullOrEmpty(model.PortText)
                ? HomeMenuModel.DefaultPort
                : model.PortText);

            bool busy = model.IsBusy || model.IsLaunching;
            bool hasSelection = model.SelectedSlotId.HasValue;
            SetEnabled(createButton, !busy);
            SetEnabled(renameButton, !busy && hasSelection);
            SetEnabled(loadButton, !busy && hasSelection);
            SetEnabled(deleteButton, !busy && hasSelection);
            SetEnabled(soloButton, model.CanLaunch);
            SetEnabled(hostButton, model.CanLaunch);
            SetEnabled(joinButton, model.CanJoin);

            SetButtonText(soloButton, model.IsLaunching ? "Startet …" : "Solo starten");
            SetButtonText(hostButton, model.IsLaunching ? "Startet …" : "Host starten · Lobby öffnen");
            SetButtonText(joinButton, model.IsLaunching ? "Beitritt läuft …" : "Beitreten");

            settingsPanel.style.display = model.IsSettingsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (model.IsSettingsOpen)
            {
                RefreshSettings();
            }

            deleteOverlay.style.display = model.IsDeleteConfirmationOpen ? DisplayStyle.Flex : DisplayStyle.None;
            deleteMessage.text = selected != null
                ? $"„{selected.DisplayName}“ wird dauerhaft entfernt. Das lässt sich nicht rückgängig machen."
                : "Der ausgewählte Spielstand wird dauerhaft entfernt.";

            root.EnableInClassList("home-busy", busy);

            if (!initialFocusApplied && root.panel != null)
            {
                initialFocusApplied = true;
                root.schedule.Execute(() =>
                {
                    VisualElement firstSlot = slotList.childCount > 0 ? slotList[0] : null;
                    (firstSlot ?? (VisualElement)createButton)?.Focus();
                });
            }
        }

        private void RefreshSettings()
        {
            LocalSettings draft = model.SettingsDraft ?? LocalSettings.Defaults;
            volumeValue.text = Mathf.RoundToInt(draft.MasterVolume * 100f) + "%";
            sensitivityValue.text = draft.MouseSensitivity.ToString("0.00");
            fullscreenValue.text = draft.Fullscreen ? "Vollbild" : "Fenster";

            string state = LocalSettingsRuntime.HasUnsavedChanges
                ? "Entwurf noch nicht gespeichert."
                : "Gespeicherte Einstellungen.";
            settingsSummary.text =
                $"{state} Master {Mathf.RoundToInt(draft.MasterVolume * 100f)}% · " +
                $"Maus {draft.MouseSensitivity:0.00} · {(draft.Fullscreen ? "Vollbild" : "Fenster")}";
        }

        private void RebuildSlotList()
        {
            slotList.Clear();
            foreach (SaveSlotSummary summary in model.Slots)
            {
                slotList.Add(BuildSlotRow(summary));
            }
        }

        private Button BuildSlotRow(SaveSlotSummary summary)
        {
            bool selected = model.SelectedSlotId.HasValue &&
                            model.SelectedSlotId.Value == summary.SlotId.Value;
            Button row = new Button(() => RunAsync(model.SelectSlotAsync(summary.SlotId)));
            row.AddToClassList("home-slot-row");
            row.EnableInClassList("slot-selected", selected);
            row.EnableInClassList("slot-unavailable", !summary.IsLoadable);
            row.tooltip = string.IsNullOrWhiteSpace(summary.ErrorMessage)
                ? summary.DisplayName
                : summary.DisplayName + ": " + summary.ErrorMessage;

            Label name = new Label(string.IsNullOrWhiteSpace(summary.DisplayName)
                ? "Unbenannter Spielstand"
                : summary.DisplayName);
            name.AddToClassList("home-slot-name");
            row.Add(name);

            Label detail = new Label(BuildSlotDetail(summary));
            detail.AddToClassList("home-slot-detail");
            row.Add(detail);

            Label status = new Label(HomeMenuModel.DescribeSlotStatus(summary));
            status.AddToClassList("home-slot-status");
            status.AddToClassList(summary.Status == SaveSlotStatus.Ok
                ? "status-ok"
                : summary.Status == SaveSlotStatus.UnsupportedSchema
                    ? "status-warning"
                    : "status-error");
            row.Add(status);

            return row;
        }

        private static string BuildSlotDetail(SaveSlotSummary summary)
        {
            string created = summary.CreatedAtUtc.HasValue
                ? summary.CreatedAtUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                : "Zeit unbekannt";
            return $"{created} · {summary.CompletedMissionCount} Mission(en)";
        }

        private void SetStatus(HomeStatusKind kind, string message)
        {
            statusLabel.text = message ?? string.Empty;
            statusLabel.RemoveFromClassList("status-info");
            statusLabel.RemoveFromClassList("status-success");
            statusLabel.RemoveFromClassList("status-warning");
            statusLabel.RemoveFromClassList("status-error");
            statusLabel.AddToClassList(StatusClass(kind));
            statusLabel.style.display = string.IsNullOrWhiteSpace(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private static string StatusClass(HomeStatusKind kind)
        {
            switch (kind)
            {
                case HomeStatusKind.Success:
                    return "status-success";
                case HomeStatusKind.Warning:
                    return "status-warning";
                case HomeStatusKind.Error:
                    return "status-error";
                default:
                    return "status-info";
            }
        }

        private static void SyncField(TextField field, string value)
        {
            bool focused = field != null &&
                           field.focusController != null &&
                           field.focusController.focusedElement == field;
            if (field != null && !focused && field.value != (value ?? string.Empty))
            {
                field.SetValueWithoutNotify(value ?? string.Empty);
            }
        }

        private static void SetEnabled(VisualElement element, bool enabled)
        {
            if (element != null)
            {
                element.SetEnabled(enabled);
            }
        }

        private static void SetButtonText(Button button, string text)
        {
            if (button != null && button.text != text)
            {
                button.text = text;
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            root.EnableInClassList("home-narrow", evt.newRect.width < 980f);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || model == null)
            {
                return;
            }

            if (model.IsDeleteConfirmationOpen)
            {
                model.CancelDeleteConfirmation();
                evt.StopPropagation();
            }
            else if (model.IsSettingsOpen)
            {
                model.CloseSettings();
                evt.StopPropagation();
            }
        }

        private async void RunAsync(Task task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                await task;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
