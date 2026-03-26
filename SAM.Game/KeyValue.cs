/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SAM.Game
{
    internal class KeyValue
    {
        private static readonly KeyValue _Invalid = new();
        public string Name = "<root>";
        public KeyValueType Type = KeyValueType.None;
        public object Value;
        public bool Valid;

        public List<KeyValue> Children = null;

        public KeyValue this[string key]
        {
            get
            {
                if (this.Children == null)
                {
                    return _Invalid;
                }

                var child = this.Children.SingleOrDefault(
                    c => string.Compare(c.Name, key, StringComparison.InvariantCultureIgnoreCase) == 0);

                if (child == null)
                {
                    return _Invalid;
                }

                return child;
            }
        }

        public string AsString(string defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            if (this.Value == null)
            {
                return defaultValue;
            }

            return this.Value.ToString();
        }

        public int AsInteger(int defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            switch (this.Type)
            {
                case KeyValueType.String:
                case KeyValueType.WideString:
                {
                    return int.TryParse((string)this.Value, out int value) == false
                        ? defaultValue
                        : value;
                }

                case KeyValueType.Int32:
                {
                    return (int)this.Value;
                }

                case KeyValueType.Float32:
                {
                    return (int)((float)this.Value);
                }

                case KeyValueType.UInt64:
                {
                    return (int)((ulong)this.Value & 0xFFFFFFFF);
                }
            }

            return defaultValue;
        }

        public float AsFloat(float defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            switch (this.Type)
            {
                case KeyValueType.String:
                case KeyValueType.WideString:
                {
                    return float.TryParse((string)this.Value, out float value) == false
                        ? defaultValue
                        : value;
                }

                case KeyValueType.Int32:
                {
                    return (int)this.Value;
                }

                case KeyValueType.Float32:
                {
                    return (float)this.Value;
                }

                case KeyValueType.UInt64:
                {
                    return (ulong)this.Value & 0xFFFFFFFF;
                }
            }

            return defaultValue;
        }

        public bool AsBoolean(bool defaultValue)
        {
            if (this.Valid == false)
            {
                return defaultValue;
            }

            switch (this.Type)
            {
                case KeyValueType.String:
                case KeyValueType.WideString:
                {
                    return int.TryParse((string)this.Value, out int value) == false
                        ? defaultValue
                        : value != 0;
                }

                case KeyValueType.Int32:
                {
                    return ((int)this.Value) != 0;
                }

                case KeyValueType.Float32:
                {
                    return ((int)((float)this.Value)) != 0;
                }

                case KeyValueType.UInt64:
                {
                    return ((ulong)this.Value) != 0;
                }
            }

            return defaultValue;
        }

        public override string ToString()
        {
            if (this.Valid == false)
            {
                return "<invalid>";
            }

            if (this.Type == KeyValueType.None)
            {
                return this.Name;
            }

            return $"{this.Name} = {this.Value}";
        }

        public static KeyValue LoadAsBinary(string path)
        {
            if (File.Exists(path) == false)
            {
                return null;
            }

            try
            {
                using (var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    KeyValue kv = new();
                    if (kv.ReadAsBinary(input) == false)
                    {
                        return null;
                    }
                    return kv;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool ReadAsBinary(Stream input)
        {
            this.Children = new();
            try
            {
                while (true)
                {
                    var type = (KeyValueType)input.ReadValueU8();

                    if (type == KeyValueType.End)
                    {
                        break;
                    }

                    KeyValue current = new()
                    {
                        Type = type,
                        Name = input.ReadStringUnicode(),
                    };

                    switch (type)
                    {
                        case KeyValueType.None:
                        {
                            current.ReadAsBinary(input);
                            break;
                        }

                        case KeyValueType.String:
                        {
                            current.Valid = true;
                            current.Value = input.ReadStringUnicode();
                            break;
                        }

                        case KeyValueType.WideString:
                        {
                            throw new FormatException("wstring is unsupported");
                        }

                        case KeyValueType.Int32:
                        {
                            current.Valid = true;
                            current.Value = input.ReadValueS32();
                            break;
                        }

                        case KeyValueType.UInt64:
                        {
                            current.Valid = true;
                            current.Value = input.ReadValueU64();
                            break;
                        }

                        case KeyValueType.Float32:
                        {
                            current.Valid = true;
                            current.Value = input.ReadValueF32();
                            break;
                        }

                        case KeyValueType.Color:
                        {
                            current.Valid = true;
                            current.Value = input.ReadValueU32();
                            break;
                        }

                        case KeyValueType.Pointer:
                        {
                            current.Valid = true;
                            current.Value = input.ReadValueU32();
                            break;
                        }

                        default:
                        {
                            throw new FormatException();
                        }
                    }

                    if (input.Position >= input.Length)
                    {
                        throw new FormatException();
                    }

                    this.Children.Add(current);
                }

                this.Valid = true;
                return input.Position == input.Length;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static KeyValue LoadAsText(string path)
        {
            if (File.Exists(path) == false)
            {
                return null;
            }

            try
            {
                string content = File.ReadAllText(path);
                return ParseText(content);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static KeyValue ParseText(string text)
        {
            int pos = 0;
            var root = new KeyValue { Valid = true, Children = new() };

            TextSkipWhitespace(text, ref pos);
            if (pos < text.Length && text[pos] == '"')
            {
                root.Name = TextReadString(text, ref pos);
                TextSkipWhitespace(text, ref pos);
                if (pos < text.Length && text[pos] == '{')
                {
                    pos++;
                    TextReadChildren(text, ref pos, root);
                }
            }

            return root;
        }

        private static void TextReadChildren(string text, ref int pos, KeyValue parent)
        {
            while (pos < text.Length)
            {
                TextSkipWhitespace(text, ref pos);
                if (pos >= text.Length) break;

                if (text[pos] == '}')
                {
                    pos++;
                    return;
                }

                if (text[pos] == '"')
                {
                    string key = TextReadString(text, ref pos);
                    TextSkipWhitespace(text, ref pos);

                    if (pos < text.Length && text[pos] == '{')
                    {
                        pos++;
                        var child = new KeyValue { Name = key, Valid = true, Children = new() };
                        TextReadChildren(text, ref pos, child);
                        parent.Children.Add(child);
                    }
                    else if (pos < text.Length && text[pos] == '"')
                    {
                        string value = TextReadString(text, ref pos);
                        parent.Children.Add(new KeyValue
                        {
                            Name = key,
                            Type = KeyValueType.String,
                            Value = value,
                            Valid = true
                        });
                    }
                }
                else
                {
                    pos++;
                }
            }
        }

        private static string TextReadString(string text, ref int pos)
        {
            if (pos >= text.Length || text[pos] != '"') return "";
            pos++;
            int start = pos;
            while (pos < text.Length && text[pos] != '"')
            {
                if (text[pos] == '\\' && pos + 1 < text.Length)
                {
                    pos += 2;
                }
                else
                {
                    pos++;
                }
            }
            string result = text.Substring(start, pos - start);
            if (pos < text.Length) pos++;
            return result;
        }

        private static void TextSkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length)
            {
                char c = text[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    pos++;
                }
                else if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/')
                {
                    while (pos < text.Length && text[pos] != '\n') pos++;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
