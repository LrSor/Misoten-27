using System;
using System.Net;
using System.Net.Sockets;

namespace Sawada.Cordex
{
    internal static class LanRoomCode
    {
        const string Prefix = "LAN";
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        const int SymbolCount = 12;

        public static bool CanEncodeAddress(IPAddress address)
            => address != null && address.AddressFamily == AddressFamily.InterNetwork && IsUnicast(address.GetAddressBytes()[0]);

        public static string Encode(IPAddress address, int port)
        {
            if (!CanEncodeAddress(address) || port < 1 || port > 65535)
                throw new ArgumentException("A LAN code requires an IPv4 address and a valid TCP port.");

            ulong endpoint = 0;
            foreach (byte part in address.GetAddressBytes()) endpoint = (endpoint << 8) | part;
            endpoint = (endpoint << 16) | (uint)port;
            ulong packed = (endpoint << 8) | Checksum(endpoint);

            var symbols = new char[SymbolCount];
            for (int i = symbols.Length - 1; i >= 0; i--)
            {
                symbols[i] = Alphabet[(int)(packed & 31)];
                packed >>= 5;
            }

            var code = new string(symbols);
            return Prefix + "-" + code.Substring(0, 4) + "-" + code.Substring(4, 4) + "-" + code.Substring(8, 4);
        }

        public static bool TryDecode(string code, out IPAddress address, out int port)
        {
            address = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim();
            if (!code.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;

            ulong packed = 0;
            int count = 0;
            for (int i = Prefix.Length; i < code.Length; i++)
            {
                char symbol = char.ToUpperInvariant(code[i]);
                if (symbol == '-' || char.IsWhiteSpace(symbol)) continue;
                if (symbol == 'O') symbol = '0';
                if (symbol == 'I' || symbol == 'L') symbol = '1';
                int digit = Alphabet.IndexOf(symbol);
                if (digit < 0 || ++count > SymbolCount) return false;
                packed = (packed << 5) | (uint)digit;
            }

            if (count != SymbolCount || packed >> 56 != 0) return false;
            ulong endpoint = packed >> 8;
            if ((byte)packed != Checksum(endpoint)) return false;
            int decodedPort = (int)(endpoint & 65535);
            byte first = (byte)(endpoint >> 40);
            if (decodedPort == 0 || !IsUnicast(first)) return false;

            address = new IPAddress(new[] { first, (byte)(endpoint >> 32), (byte)(endpoint >> 24), (byte)(endpoint >> 16) });
            port = decodedPort;
            return true;
        }

        static bool IsUnicast(byte firstOctet) => firstOctet > 0 && firstOctet < 224;

        // CRC-8 over the four address bytes and two port bytes catches typical typos.
        static byte Checksum(ulong endpoint)
        {
            int crc = 0;
            for (int shift = 40; shift >= 0; shift -= 8)
            {
                crc ^= (byte)(endpoint >> shift);
                for (int bit = 0; bit < 8; bit++)
                    crc = ((crc << 1) ^ ((crc & 0x80) != 0 ? 0x07 : 0)) & 0xff;
            }
            return (byte)crc;
        }
    }
}
