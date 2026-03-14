using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace OmniTactica.AppCode.Services
{
    public class NavigationHistoryService : IDisposable
    {
        private readonly NavigationManager _navigation;
        private readonly List<string> _history = new();
        private string? _suppressedTarget;
        private bool _initialized;

        public NavigationHistoryService(NavigationManager navigation)
        {
            _navigation = navigation;
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _navigation.LocationChanged += HandleLocationChanged;
            PushIfNew(GetCurrentRelativeUri());
        }

        public string GetBackTarget(string fallbackHref = "/")
        {
            Initialize();
            return _history.Count >= 2 ? _history[^2] : NormalizeRelativeUri(fallbackHref);
        }

        public void GoBack(string fallbackHref = "/")
        {
            Initialize();

            if (_history.Count >= 2)
            {
                _history.RemoveAt(_history.Count - 1);
                var target = _history[^1];
                _suppressedTarget = target;
                _navigation.NavigateTo(target);
                return;
            }

            _navigation.NavigateTo(NormalizeRelativeUri(fallbackHref));
        }

        private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            var relativeUri = NormalizeRelativeUri(_navigation.ToBaseRelativePath(e.Location));

            if (!string.IsNullOrWhiteSpace(_suppressedTarget) &&
                string.Equals(relativeUri, _suppressedTarget, StringComparison.OrdinalIgnoreCase))
            {
                _suppressedTarget = null;
                return;
            }

            PushIfNew(relativeUri);
        }

        private void PushIfNew(string relativeUri)
        {
            if (_history.Count == 0 || !string.Equals(_history[^1], relativeUri, StringComparison.OrdinalIgnoreCase))
            {
                _history.Add(relativeUri);
            }
        }

        private string GetCurrentRelativeUri() => NormalizeRelativeUri(_navigation.ToBaseRelativePath(_navigation.Uri));

        private static string NormalizeRelativeUri(string? relativeUri)
        {
            if (string.IsNullOrWhiteSpace(relativeUri))
            {
                return "/";
            }

            return relativeUri.StartsWith('/') ? relativeUri : $"/{relativeUri}";
        }

        public void Dispose()
        {
            _navigation.LocationChanged -= HandleLocationChanged;
        }
    }
}
