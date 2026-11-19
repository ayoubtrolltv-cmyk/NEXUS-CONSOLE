namespace Nexus.Detection;

/// <summary>Lecteur minimal du format texte Valve (VDF/ACF) : paires "clé" "valeur" et blocs imbriqués.</summary>
public static class VdfParser
{
    public static Dictionary<string, object> Parse(string text)
    {
        var reader = new Reader(text);
        var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        ParseBody(reader, root);
        return root;
    }

    /// <summary>Descend dans les blocs imbriqués ; retourne le bloc, la chaîne trouvée, ou null.</summary>
    public static object? Dig(Dictionary<string, object> dict, params string[] path)
    {
        object? cur = dict;
        foreach (var key in path)
        {
            if (cur is not Dictionary<string, object> d || !d.TryGetValue(key, out cur)) return null;
        }
        return cur;
    }

    public static string? GetString(Dictionary<string, object> dict, params string[] path) => Dig(dict, path) as string;

    private static void ParseBody(Reader r, Dictionary<string, object> into)
    {
        while (true)
        {
            var key = r.Next();
            if (key is null || key.Value.Kind == '}') return;
            if (key.Value.Kind != 'S') continue;

            var val = r.Next();
            if (val is null) return;
            if (val.Value.Kind == '{')
            {
                var child = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                ParseBody(r, child);
                into[key.Value.Text] = child;
            }
            else if (val.Value.Kind == 'S')
            {
                into[key.Value.Text] = val.Value.Text;
            }
        }
    }

    private sealed class Reader
    {
        private readonly string _s;
        private int _i;
        public Reader(string s) => _s = s;

        public (char Kind, string Text)? Next()
        {
            while (_i < _s.Length)
            {
                var c = _s[_i];
                if (char.IsWhiteSpace(c)) { _i++; continue; }
                if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                {
                    while (_i < _s.Length && _s[_i] != '\n') _i++;
                    continue;
                }
                if (c == '{' || c == '}') { _i++; return (c, ""); }
                if (c == '"')
                {
                    _i++;
                    var sb = new System.Text.StringBuilder();
                    while (_i < _s.Length && _s[_i] != '"')
                    {
                        if (_s[_i] == '\\' && _i + 1 < _s.Length)
                        {
                            _i++;
                            sb.Append(_s[_i] switch { 'n' => '\n', 't' => '\t', var o => o });
                        }
                        else sb.Append(_s[_i]);
                        _i++;
                    }
                    _i++;
                    return ('S', sb.ToString());
                }
                _i++;   // caractère inattendu : ignoré
            }
            return null;
        }
    }
}
