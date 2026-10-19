using System.Net;
using System.Net.Sockets;
using FluentModbus;

internal static class Program
{
    private static int _transactionId = 0;

    private static void Main(string[] args)
    {
        if (args.Length < 5)
        {
            Console.WriteLine("用法: ModbusClearBit <host> <port> <unitId> <registerAddress> <bitIndexList>");
            Console.WriteLine("範例: ModbusClearBit 192.168.0.100 502 1 0 3");
            Console.WriteLine("或: ModbusClearBit 192.168.0.100 502 1 0 0,3,5");
            Console.WriteLine("或: ModbusClearBit 192.168.0.100 502 1 0 0-3 (含 0 至 3)");
            Console.WriteLine("說明: registerAddress 使用 0-based (例如 0 對應於 40001)，bitIndex 範圍 0..15，可為逗號或範圍表示法");
            return;
        }

        string host = args[0];
        if (!int.TryParse(args[1], out var port)) port = 502;
        if (!int.TryParse(args[2], out var unitId)) unitId = 1;
        if (!int.TryParse(args[3], out var registerAddress))
        {
            Console.Error.WriteLine("registerAddress 需要為整數");
            return;
        }
        var bitSpec = args[4];
        if (!TryParseBitList(bitSpec, out var bits))
        {
            Console.Error.WriteLine("bitIndexList 格式錯誤。可為單一數字、逗號分隔或範圍 (例如 0,3,5 或 0-3)。且每個 bit 必須在 0..15 之間。");
            return;
        }

        try
        {
            using var client = new ModbusTcpClient();
            client.Connect(new IPEndPoint(IPAddress.Parse(host), port), ModbusEndianness.BigEndian);

            // 讀取單一 holding
            var reg = client.ReadHoldingRegisters<ushort>((byte)unitId, (ushort)registerAddress, 1);
            var currentValue = reg[0];
            
            Console.WriteLine($"目前 register[{registerAddress}] = 0x{currentValue:X4} ({currentValue})");

            // 清除指定 bit(s)
            ushort mask = 0;
            foreach (var b in bits)
            {
                mask |= (ushort)(1 << b);
            }
            ushort newValue = (ushort)(currentValue & (ushort)~mask);
            if (newValue == currentValue)
            {
                Console.WriteLine("指定 bit(s) 已經是 0，無需寫入。");
                return;
            }

            // 使用 Write Single Register (func 6)
            client.WriteSingleRegister((byte)unitId, (ushort)registerAddress, newValue);

            Console.WriteLine($"已寫入 register[{registerAddress}] = 0x{newValue:X4} ({newValue})，清除 bits: {string.Join(",", bits)}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"錯誤: {ex.Message}");
        }
    }


    private static bool TryParseBitList(string input, out int[] bits)
    {
        bits = Array.Empty<int>();
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new System.Collections.Generic.List<int>();
        foreach (var p in parts)
        {
            if (p.Contains('-'))
            {
                var rng = p.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (rng.Length != 2) return false;
                if (!int.TryParse(rng[0], out var a)) return false;
                if (!int.TryParse(rng[1], out var b)) return false;
                if (a > b) return false;
                for (int i = a; i <= b; i++)
                {
                    if (i < 0 || i > 15) return false;
                    if (!list.Contains(i)) list.Add(i);
                }
            }
            else
            {
                if (!int.TryParse(p, out var v)) return false;
                if (v < 0 || v > 15) return false;
                if (!list.Contains(v)) list.Add(v);
            }
        }
        bits = list.ToArray();
        return bits.Length > 0;
    }

}
