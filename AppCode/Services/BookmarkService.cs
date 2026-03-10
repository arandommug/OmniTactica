using System.Text.Json;
using OmniTactica.AppCode.Models.Core;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Service for managing user bookmarks with local storage persistence.
    /// </summary>
    public class BookmarkService
    {
        private const string StorageKey = "omnitactica_bookmarks";
        private List<Bookmark> _bookmarks = new();

        public event Action? OnBookmarksChanged;

        public BookmarkService()
        {
            LoadBookmarks();
        }

        public List<Bookmark> GetAllBookmarks() => _bookmarks.ToList();

        public List<Bookmark> GetBookmarksByType(BookmarkType type) =>
            _bookmarks.Where(b => b.Type == type).ToList();

        public bool IsBookmarked(BookmarkType type, string entityId) =>
            _bookmarks.Any(b => b.Type == type && b.EntityId == entityId);

        public async Task AddBookmarkAsync(Bookmark bookmark)
        {
            if (!IsBookmarked(bookmark.Type, bookmark.EntityId))
            {
                _bookmarks.Add(bookmark);
                await SaveBookmarksAsync();
                OnBookmarksChanged?.Invoke();
            }
        }

        public async Task RemoveBookmarkAsync(string bookmarkId)
        {
            var bookmark = _bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
            if (bookmark != null)
            {
                _bookmarks.Remove(bookmark);
                await SaveBookmarksAsync();
                OnBookmarksChanged?.Invoke();
            }
        }

        public async Task RemoveBookmarkByEntityAsync(BookmarkType type, string entityId)
        {
            var bookmark = _bookmarks.FirstOrDefault(b => b.Type == type && b.EntityId == entityId);
            if (bookmark != null)
            {
                _bookmarks.Remove(bookmark);
                await SaveBookmarksAsync();
                OnBookmarksChanged?.Invoke();
            }
        }

        public async Task ClearAllBookmarksAsync()
        {
            _bookmarks.Clear();
            await SaveBookmarksAsync();
            OnBookmarksChanged?.Invoke();
        }

        private void LoadBookmarks()
        {
            try
            {
                var json = Preferences.Get(StorageKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    _bookmarks = JsonSerializer.Deserialize<List<Bookmark>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BookmarkService] Error loading bookmarks: {ex.Message}");
                _bookmarks = new();
            }
        }

        private async Task SaveBookmarksAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_bookmarks);
                Preferences.Set(StorageKey, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BookmarkService] Error saving bookmarks: {ex.Message}");
            }
        }
    }
}
