namespace MetaTSUK
{
    // ushort: в файлах чанков блок и так хранится в 2 байтах, а в памяти чанк 64^3 весит 512 КБ вместо 1 МБ
    public enum Block : ushort
    {
        None = 0,
        Transparent = 1,
        Object_Dark = 2,
        Object_Light = 3,
        Object_Translucent = 4,
        Object_Solid = 5,
        Object_Copybook = 6,
        Object_Debug = 7,
        Ground = 8,
        Opaque = 9,
        Detonator = 10,
        Nuke = 11,
        WorldBarrier = 12,
        Computer = 13,
    }
}
