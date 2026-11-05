using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;


// Это ваш Ray класс, переписанный для GPU
public static class RaytracingKernel
{
    public static void Trace(
        Index2D index,           // Координаты пикселя (x, y)
        ArrayView<int> worldData,// Плоский массив блоков
        int worldWidth,
        int worldHeight,
        int worldDepth,
        ArrayView<float> output, // Выходной буфер (RGB)
        float cameraX,
        float cameraY,
        float cameraZ,
        float phiRad,
        float thetaRad,
        float renderDistance,
        float quality
    )
    {
        // === ВАШ ОРИГИНАЛЬНЫЙ КОД, ПЕРЕНЕСЕННЫЙ ПОЧТИ 1-В-1 ===

        // Вычисляем направление луча для этого пикселя
        float dirX = XMath.Sin(thetaRad) * XMath.Cos(phiRad);
        float dirY = XMath.Sin(thetaRad);
        float dirZ = XMath.Cos(thetaRad) * XMath.Sin(phiRad);

        float i = 0;
        int hitBlock = 0;
        float hitX = 0, hitY = 0, hitZ = 0;

        do
        {
            i += quality;

            // Позиция на луче
            float x = cameraX + i * dirX;
            float y = cameraY + i * dirY;
            float z = cameraZ + i * dirZ;

            // Преобразуем в целые координаты блока
            int bx = (int)x;
            int by = (int)y;
            int bz = (int)z;

            // Проверка границ мира
            if (bx >= 0 && bx < worldWidth &&
                by >= 0 && by < worldHeight &&
                bz >= 0 && bz < worldDepth)
            {
                // === ГЛАВНОЕ ОТЛИЧИЕ ОТ CPU: БЕЗ DICTIONARY! ===
                int idx = bx + by * worldWidth + bz * worldWidth * worldHeight;
                int block = worldData[idx];

                if (block != 0) // 0 = Transparent
                {
                    hitBlock = block;
                    hitX = x; hitY = y; hitZ = z;
                    break;
                }
            }
        }
        while (i <= renderDistance);

        // === ВЫВОД РЕЗУЛЬТАТА ===
        long pixelIndex = index.X + index.Y * output.Extent.X;

        if (hitBlock != 0)
        {
            // Ваша логика цвета блока
            output[pixelIndex * 3 + 0] = 1.0f; // R
            output[pixelIndex * 3 + 1] = 0.5f; // G
            output[pixelIndex * 3 + 2] = 0.0f; // B
        }
        else
        {
            // Небо
            output[pixelIndex * 3 + 0] = 0.0f;
            output[pixelIndex * 3 + 1] = 0.0f;
            output[pixelIndex * 3 + 2] = 0.0f;
        }
    }
}