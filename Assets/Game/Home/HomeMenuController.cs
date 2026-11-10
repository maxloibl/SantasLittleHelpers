using System;
using System.Threading.Tasks;
using SantasLittleHelpers.Savegames;
using UnityEngine;
using UnityEngine.UIElements;

namespace SantasLittleHelpers.Home
{
    /// <summary>
    /// Public home-screen integration point for #41.
    /// Set the real slot store and launch delegate; the controller owns only UI/state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HomeMenuController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;

        private HomeMenuModel model;
        private HomeMenuView view;
        private Func<HomeLaunchRequest, Task<HomeLaunchResult>> launchHandler;
        private bool initializing;

        public Func<HomeLaunchRequest, Task<HomeLaunchResult>> Launch
        {
            get => launchHandler;
            set
            {
                launchHandler = value;
                model?.SetLauncher(value);
            }
        }

        public HomeMenuModel Model => model;

        private void Awake()
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }

            SetStore(new SaveSlotStore());
        }

        private void OnEnable()
        {
            EnsureView();
            BeginInitialize();
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.Dispose();
                view = null;
            }
        }

        public void SetStore(SaveSlotStore store)
        {
            if (model != null)
            {
                model.LaunchSucceeded -= OnLaunchSucceeded;
            }

            model = new HomeMenuModel(store ?? new SaveSlotStore(), launchHandler);
            model.LaunchSucceeded += OnLaunchSucceeded;

            if (view != null)
            {
                view.SetModel(model);
            }

            if (isActiveAndEnabled)
            {
                BeginInitialize();
            }
        }

        public void Show(string notice = null)
        {
            EnsureView();
            SetRootVisible(true);
            model?.ShowNotice(notice);
            BeginInitialize();
        }

        public void Hide()
        {
            SetRootVisible(false);
        }

        private void EnsureView()
        {
            if (view != null)
            {
                return;
            }

            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }

            if (document == null || document.rootVisualElement == null)
            {
                Debug.LogError("HOME_MENU_ERROR uiDocument=missing");
                return;
            }

            view = new HomeMenuView(document.rootVisualElement, styleSheet);
            view.SetModel(model);
        }

        private void SetRootVisible(bool visible)
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }

            if (document != null && document.rootVisualElement != null)
            {
                document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void BeginInitialize()
        {
            if (model == null || initializing)
            {
                return;
            }

            initializing = true;
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                await model.InitializeAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                model.ShowNotice("Der Startbildschirm konnte nicht vollständig geladen werden.");
            }
            finally
            {
                initializing = false;
            }
        }

        private void OnLaunchSucceeded(HomeLaunchRequest request)
        {
            Debug.Log("HOME_MENU_LAUNCH result=Accepted " + request);
            Hide();
        }
    }
}
