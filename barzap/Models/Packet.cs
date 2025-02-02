using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace barzap.Models {

    public class Packet {

        public string Op { get; set; } = "";

        public int DataSize { get; set; }

        public string Data { get; set; } = "";

        private Dictionary<string, string> Fields = new();

        private bool _FieldsSet = false;

        public string? GetField(string field) {
            if (_FieldsSet == false) {
                SetFields();
            }

            return Fields.GetValueOrDefault(field);
        }

        private void SetFields() {

            PacketFieldParseState state = PacketFieldParseState.FIELD_NAME;

            //a1b2c"hi"

            string iter = "";
            string fieldName = "";
            bool backSlash = false;

            foreach (char c in Data) {
                //Console.WriteLine($"iter={iter}, c={c}, state={state}");
                if (state == PacketFieldParseState.FIELD_NAME) {
                    if (char.IsAsciiDigit(c)) {
                        //Console.WriteLine($"start of number field");
                        fieldName = iter;
                        iter = "";
                        iter += c;
                        state = PacketFieldParseState.FIELD_READ_NUMBER;
                    } else if (c == '"') {
                        //Console.WriteLine($"start of string field");
                        fieldName = iter;
                        iter = "";
                        state = PacketFieldParseState.FIELD_READ_STRING;
                    } else {
                        iter += c;
                    }
                } else if (state == PacketFieldParseState.FIELD_READ_NUMBER) {
                    if (!char.IsAsciiDigit(c) && c != '.') {
                        //Console.WriteLine($"end of number");
                        Fields.Add(fieldName, iter);
                        state = PacketFieldParseState.FIELD_NAME;
                        iter = "";
                        iter += c;
                    } else {
                        iter += c;
                    }
                } else if (state == PacketFieldParseState.FIELD_READ_STRING) {
                    if (backSlash == false && c == '"') {
                        //Console.WriteLine($"end of string");
                        Fields.Add(fieldName, iter);
                        state = PacketFieldParseState.FIELD_NAME;
                        iter = "";
                    } else {
                        backSlash = c == '\\';

                        if (backSlash == false) {
                            iter += c;
                        }
                    }
                } else {
                    throw new Exception($"unexpected state {state}");
                }
            }

            if (state == PacketFieldParseState.FIELD_READ_NUMBER) {
                Fields.Add(fieldName, iter);
            }

            _FieldsSet = true;
        }


    }

    public static class PacketExtensions {

        public static long ReadLong(this Packet packet, string fieldName) {
            return long.Parse(packet.GetField(fieldName) ?? throw new Exception($"failed to find field '{fieldName}' in '{packet.Data}'"));
        }

        public static long? ReadNullableLong(this Packet packet, string fieldName) {
            string? value = packet.GetField(fieldName);
            if (value == null) {
                return null;
            }

            return long.Parse(value);
        }

        public static decimal ReadDecimal(this Packet packet, string fieldName) {
            return decimal.Parse(packet.GetField(fieldName) ?? throw new Exception($"failed to find field '{fieldName}' in '{packet.Data}'"));
        }

    }

    enum PacketFieldParseState {

        START,

        FIELD_NAME,

        FIELD_READ_NUMBER,

        FIELD_READ_STRING,

    }


}
