using System;
using System.Numerics;
using System.Text;

namespace FFRadarBuddy
{
    public class MemoryLayout
    {
        // actor and target => based on project
        // https://github.com/FFXIVAPP/sharlayan
        //
        // camera: researched on my own

        public static MemoryPath memPathActors = new MemoryPathSignature("488b420848c1e8033da701000077??8bc0488d0d", 0);
        public static MemoryPath memPathTarget = new MemoryPathSignature("5fc3483935????????75??483935", -16);
        public static MemoryPath memPathCamera = new MemoryPath(0x1e7b880, 0);
        public static MemoryPath memPathConditionFlag = new MemoryPath(0x1ec10b0);

        public class ActorConsts
        {
            public static int Size = 9200;

            public static int Name = 48;             // string
            public static int ActorIdA = 116;        // uint32
            public static int ActorIdB = 120;        // uint32
            public static int NpcId = 128;           // uint32
            public static int Type = 140;            // uint8
            public static int SubType = 141;         // uint8
            public static int Flags = 148;           // uint8
            public static int Position = 160;        // 3x float
            public static int HitBoxRadius = 192;    // float
        }

        public class TargetConsts
        {
            public static int Size = 48;
            public static int Current = 40;          // uint64 (actor ptr)
        }

        public class CameraConsts
        {
            public static int Size = 0x1d0;
            public static int Position = 0x1b0;       // 3x float
            public static int Target = 0x1c0;         // 3x float
            public static int Distance = 0x114;      // float
            public static int Fov = 0x120;           // float
        }

        public class ConditionFlagConsts
        {
            public static int Size = 95; // at least
            public static int OccupiedInCutSceneEvent = 35;
            public static int WatchingCutscene = 58;
            public static int WatchingCutscene78 = 78;
        }

        public enum ActorType : byte
        {
            None = 0,
            Player = 1,
            Monster = 2,
            Npc = 3,
            Treasure = 4,
            Aetheryte = 5,
            Gathering = 6,
            Interaction = 7,
            Mount = 8,
            Minion = 9,
            Retainer = 10,
            Housing = 12,
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public class ActorData
        {
            public string Name;
            public uint ActorIdA;
            public uint ActorIdB;
            public uint NpcId;
            public ActorType Type;
            public byte SubType;
            public byte Flags;
            public Vector3 Position = new Vector3();
            public float Radius;
            public long UniqueId;
            public bool IsHidden;
#if DEBUG
            public byte[] prevBytes;
#endif // DEBUG

            public ActorData() { }
            public ActorData(byte[] bytes) { Set(bytes); }

            public void SetIdOnly(byte[] bytes)
            {
                ActorIdA = BitConverter.ToUInt32(bytes, ActorConsts.ActorIdA);
                ActorIdB = BitConverter.ToUInt32(bytes, ActorConsts.ActorIdB);
                NpcId = BitConverter.ToUInt32(bytes, ActorConsts.NpcId);

                UniqueId = (ActorIdB != 0) ? ActorIdB : ActorIdA;
                UniqueId <<= 32;
                UniqueId |= NpcId;
            }

            public void SetDataOnly(byte[] bytes)
            {
                Type = (ActorType)bytes[ActorConsts.Type];
                SubType = bytes[ActorConsts.SubType];

                // interaction: hidden/used: 2b, 2f, 28
                // gathering:   hidden/used: 3c, bc
                // interaction: available: bf
                // gathering:   available: 3f, bf
                // interaction: currently used: ff
                // gathering:   currently used: 7f
                Flags = bytes[ActorConsts.Flags];
                IsHidden = ((Flags & 0xf0) == 0x20) || ((Flags & 0xf) == 0xc);
#if DEBUG
                if (Type == ActorType.Interaction || Type == ActorType.Gathering)
                {
                    if (prevBytes != null && prevBytes.Length == bytes.Length)
                    {
                        for (int Idx = 0; Idx < bytes.Length; Idx++)
                        {
                            if (prevBytes[Idx] != bytes[Idx])
                            {
                                Logger.WriteLine("Scan diff! [" + Idx + "] 0x" + prevBytes[Idx].ToString("x2") + " -> 0x" + bytes[Idx].ToString("x2"));
                            }
                        }
                    }
                    else
                    {
                        prevBytes = new byte[bytes.Length];
                    }

                    Array.Copy(bytes, prevBytes, bytes.Length);
                }
#endif // DEBUG

                Position.X = BitConverter.ToSingle(bytes, ActorConsts.Position);
                Position.Y = BitConverter.ToSingle(bytes, ActorConsts.Position + 4);
                Position.Z = BitConverter.ToSingle(bytes, ActorConsts.Position + 8);
                Radius = BitConverter.ToSingle(bytes, ActorConsts.HitBoxRadius);

                // read string at Actor.Name
                int useSize = Math.Max(255, bytes.Length - ActorConsts.Name);
                byte[] stringBytes = new byte[useSize];
                for (int Idx = 0; Idx < useSize; Idx++)
                {
                    if (bytes[ActorConsts.Name + Idx] == 0)
                    {
                        Array.Resize(ref stringBytes, Idx);
                        break;
                    }

                    stringBytes[Idx] = bytes[ActorConsts.Name + Idx];
                }

                Name = Encoding.UTF8.GetString(stringBytes);
            }

            public void Set(byte[] bytes)
            {
                SetIdOnly(bytes);
                SetDataOnly(bytes);
            }
        }

        public class TargetData
        {
            public long CurrentAddress;

            public TargetData() { }
            public TargetData(byte[] bytes) { Set(bytes); }

            public void Set(byte[] bytes)
            {
                CurrentAddress = BitConverter.ToInt64(bytes, TargetConsts.Current);
            }
        }

        public class CameraData
        {
            public float Fov;
            public float Distance;
            public Vector3 Position = new Vector3();
            public Vector3 Target = new Vector3();

            public CameraData() { }
            public CameraData(byte[] bytes) { Set(bytes); }

            public void Set(byte[] bytes)
            {
                Fov = BitConverter.ToSingle(bytes, CameraConsts.Fov);
                Distance = BitConverter.ToSingle(bytes, CameraConsts.Distance);
                Position.X = BitConverter.ToSingle(bytes, CameraConsts.Position);
                Position.Y = BitConverter.ToSingle(bytes, CameraConsts.Position + 4);
                Position.Z = BitConverter.ToSingle(bytes, CameraConsts.Position + 8);
                Target.X = BitConverter.ToSingle(bytes, CameraConsts.Target);
                Target.Y = BitConverter.ToSingle(bytes, CameraConsts.Target + 4);
                Target.Z = BitConverter.ToSingle(bytes, CameraConsts.Target + 8);
            }
        }

        public class ConditionFlagData
        {
            public byte[] ConditionFlags;
            private bool OccupiedInCutSceneEvent;
            private bool WatchingCutscene;
            private bool WatchingCutscene78;

            public ConditionFlagData() { }
            public ConditionFlagData(byte[] bytes) { Set(bytes); }

            public bool Set(byte[] bytes)
            {
                if (!ByteArrayCompare(ConditionFlags, bytes))
                {
                    ConditionFlags = bytes;
                    OccupiedInCutSceneEvent = BitConverter.ToBoolean(bytes, ConditionFlagConsts.OccupiedInCutSceneEvent);
                    WatchingCutscene = BitConverter.ToBoolean(bytes, ConditionFlagConsts.WatchingCutscene);
                    WatchingCutscene78 = BitConverter.ToBoolean(bytes, ConditionFlagConsts.WatchingCutscene78);
                    return true;
                }
                return false;
            }

            private static bool ByteArrayCompare(byte[] a1, byte[] a2)
            {
                if ((a1 == null) != (a2 == null))
                    return false;

                if (a1.Length != a2.Length)
                    return false;

                for (int i = 0; i < a1.Length; i++)
                    if (a1[i] != a2[i])
                        return false;

                return true;
            }

            public bool IsWatchingCutscene()
            {
                return OccupiedInCutSceneEvent || WatchingCutscene || WatchingCutscene78;
            }
        }
    }
}
