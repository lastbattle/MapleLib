using MapleLib.WzLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MapleLib.Img
{
    /// <summary>
    /// A dictionary that stores image names and loads WzImage objects on-demand.
    /// This dramatically reduces memory usage during initialization by not loading
    /// all images upfront.
    /// </summary>
    public class LazyWzImageDictionary : IDictionary<string, WzImage>
    {
        private readonly HashSet<string> _names;
        private readonly Func<string, WzImage> _loader;
        private readonly Dictionary<string, WzImage> _loadedCache;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new lazy-loading dictionary.
        /// </summary>
        /// <param name="loader">Function to load a WzImage by name. Should return null if not found.</param>
        public LazyWzImageDictionary(Func<string, WzImage> loader)
        {
            _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _loadedCache = new Dictionary<string, WzImage>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers an available image name (does not load it).
        /// </summary>
        public void RegisterName(string name)
        {
            lock (_lock)
            {
                _names.Add(name);
            }
        }

        /// <summary>
        /// Registers multiple image names at once.
        /// </summary>
        public void RegisterNames(IEnumerable<string> names)
        {
            lock (_lock)
            {
                foreach (var name in names)
                {
                    _names.Add(name);
                }
            }
        }

        /// <summary>
        /// Gets or sets a WzImage by name. Getting triggers lazy loading.
        /// </summary>
        public WzImage this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                    return null;

                lock (_lock)
                {
                    // Check if already loaded
                    if (_loadedCache.TryGetValue(key, out var cached))
                        return cached;

                    // Check if name is registered
                    if (!_names.Contains(key))
                        return null;

                    // Load on demand
                    try
                    {
                        var image = _loader(key);
                        if (image != null)
                        {
                            _loadedCache[key] = image;
                        }
                        return image;
                    }
                    catch
                    {
                        // If loading fails, return null rather than crashing
                        return null;
                    }
                }
            }
            set
            {
                if (string.IsNullOrEmpty(key))
                    return;

                lock (_lock)
                {
                    _names.Add(key);
                    if (value != null)
                        _loadedCache[key] = value;
                    else
                        _loadedCache.Remove(key);
                }
            }
        }

        /// <summary>
        /// Gets all registered names (without loading).
        /// </summary>
        public ICollection<string> Keys
        {
            get
            {
                lock (_lock)
                {
                    return _names.ToList();
                }
            }
        }

        /// <summary>
        /// Gets all loaded values. Warning: This loads ALL images!
        /// </summary>
        public ICollection<WzImage> Values
        {
            get
            {
                lock (_lock)
                {
                    // Load all images - expensive operation!
                    foreach (var name in _names)
                    {
                        if (!_loadedCache.ContainsKey(name))
                        {
                            var image = _loader(name);
                            if (image != null)
                                _loadedCache[name] = image;
                        }
                    }
                    return _loadedCache.Values.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the count of registered names (not loaded images).
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _names.Count;
                }
            }
        }

        /// <summary>
        /// Gets the number of actually loaded images.
        /// </summary>
        public int LoadedCount
        {
            get
            {
                lock (_lock)
                {
                    return _loadedCache.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(string key, WzImage value)
        {
            this[key] = value;
        }

        public void Add(KeyValuePair<string, WzImage> item)
        {
            this[item.Key] = item.Value;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _names.Clear();
                _loadedCache.Clear();
            }
        }

        public bool Contains(KeyValuePair<string, WzImage> item)
        {
            return ContainsKey(item.Key);
        }

        public bool ContainsKey(string key)
        {
            lock (_lock)
            {
                return _names.Contains(key);
            }
        }

        public void CopyTo(KeyValuePair<string, WzImage>[] array, int arrayIndex)
        {
            lock (_lock)
            {
                int i = arrayIndex;
                foreach (var name in _names)
                {
                    array[i++] = new KeyValuePair<string, WzImage>(name, this[name]);
                }
            }
        }

        public IEnumerator<KeyValuePair<string, WzImage>> GetEnumerator()
        {
            // IMPORTANT: Do NOT load values during enumeration!
            // This prevents loading all images when iterating (which defeats lazy loading).
            // Consumers who only need keys should use .Keys property.
            // Consumers who need values should access via indexer after iteration.
            List<string> namesCopy;
            lock (_lock)
            {
                namesCopy = _names.ToList();
            }

            foreach (var name in namesCopy)
            {
                // Return null for value - consumers must use indexer to get actual value
                // This is intentional to prevent loading ALL images during foreach loops
                yield return new KeyValuePair<string, WzImage>(name, null);
            }
        }

        public bool Remove(string key)
        {
            lock (_lock)
            {
                _loadedCache.Remove(key);
                return _names.Remove(key);
            }
        }

        public bool Remove(KeyValuePair<string, WzImage> item)
        {
            return Remove(item.Key);
        }

        public bool TryGetValue(string key, out WzImage value)
        {
            // TryGetValue should be consistent with ContainsKey
            // If key is registered, return true even if value loads as null
            if (string.IsNullOrEmpty(key))
            {
                value = null;
                return false;
            }

            lock (_lock)
            {
                if (!_names.Contains(key))
                {
                    value = null;
                    return false;
                }
            }

            // Key exists, try to load value
            value = this[key];
            return true; // Return true because key exists, even if value is null
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Clears only the loaded cache, keeping registered names.
        /// Use this to free memory while keeping the list of available images.
        /// </summary>
        public void ClearLoadedCache()
        {
            lock (_lock)
            {
                _loadedCache.Clear();
            }
        }
    }
}
