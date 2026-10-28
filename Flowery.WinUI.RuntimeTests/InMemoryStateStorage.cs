using System;
using System.Collections.Generic;
using System.Linq;
using Flowery.Services;

namespace Flowery.WinUI.RuntimeTests
{
    internal sealed class InMemoryStateStorage : IStateStorage
    {
        private readonly Dictionary<string, List<string>> _store = new(StringComparer.Ordinal);

        public IReadOnlyList<string> LoadLines(string key)
        {
            return _store.TryGetValue(key, out var lines) ? lines : Array.Empty<string>();
        }

        /// <summary>How often <see cref="SaveLines"/> was called.</summary>
        public int SaveCount { get; private set; }

        public void SaveLines(string key, IEnumerable<string> lines)
        {
            SaveCount++;
            _store[key] = new List<string>(lines);
        }

        public void Delete(string key)
        {
            _store.Remove(key);
        }

        public void Rename(string sourceKey, string targetKey)
        {
            if (_store.TryGetValue(sourceKey, out var lines))
            {
                _store[targetKey] = new List<string>(lines);
                _store.Remove(sourceKey);
            }
        }

        public IEnumerable<string> GetKeys(string prefix)
        {
            foreach (var key in _store.Keys.ToList())
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    yield return key;
            }
        }
    }
}
