using OmniTactica.AppCode.Services;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica
{
    public partial class App : Application
    {
        public App(AbilityRulesService abilityRulesService)
        {
            InitializeComponent();

            // Initialize the ability rules service asynchronously
            Task.Run(async () =>
            {
                await abilityRulesService.InitializeAsync();
                WeaponAbilityParser.Initialize(abilityRulesService);
            });
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "OmniTactica" };
        }
    }
}
