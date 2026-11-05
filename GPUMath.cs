using ComputeSharp;
using ComputeSharp.__Internals;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MetaTSUK
{
    public class GPUMath
    {
        [AutoConstructor]
        public readonly partial struct SinShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> output;  // Буфер для результатов
            public readonly ReadWriteBuffer<float> input;   // Буфер с исходными данными

            public SinShader(ReadWriteBuffer<float> i, ReadWriteBuffer<float> o)
            {
                input = i;
                output = o;
            }

            public void BuildHlslSource(out ArrayPoolStringBuilder builder, int threadsX, int threadsY, int threadsZ)
            {
                throw new NotImplementedException();
            }

            public void Execute()
            {
                // ThreadIds.X — это индекс текущего элемента (от 0 до N-1)
                float value = input[ThreadIds.X];
                output[ThreadIds.X] = (float)Math.Sin(value);
            }

            public int GetDispatchId()
            {
                throw new NotImplementedException();
            }

            public void LoadBytecode<TLoader>(ref TLoader loader, int threadsX, int threadsY, int threadsZ) where TLoader : struct, IBytecodeLoader
            {
                throw new NotImplementedException();
            }

            public void LoadDispatchData<TLoader>(ref TLoader loader, GraphicsDevice device, int x, int y, int z) where TLoader : struct, IDispatchDataLoader
            {
                throw new NotImplementedException();
            }

            public void LoadDispatchMetadata<TLoader>(ref TLoader loader, out IntPtr result) where TLoader : struct, IDispatchMetadataLoader
            {
                throw new NotImplementedException();
            }
        }

        [AutoConstructor]
        public readonly partial struct CoSinShader : IComputeShader
        {
            public readonly ReadWriteBuffer<float> output;  // Буфер для результатов
            public readonly ReadWriteBuffer<float> input;   // Буфер с исходными данными

            public CoSinShader(ReadWriteBuffer<float> i, ReadWriteBuffer<float> o)
            {
                input = i;
                output = o;
            }

            public void BuildHlslSource(out ArrayPoolStringBuilder builder, int threadsX, int threadsY, int threadsZ)
            {
                throw new NotImplementedException();
            }

            public void Execute()
            {
                // ThreadIds.X — это индекс текущего элемента (от 0 до N-1)
                float value = input[ThreadIds.X];
                output[ThreadIds.X] = (float)Math.Sin(value);
            }

            public int GetDispatchId()
            {
                throw new NotImplementedException();
            }

            public void LoadBytecode<TLoader>(ref TLoader loader, int threadsX, int threadsY, int threadsZ) where TLoader : struct, IBytecodeLoader
            {
                throw new NotImplementedException();
            }

            public void LoadDispatchData<TLoader>(ref TLoader loader, GraphicsDevice device, int x, int y, int z) where TLoader : struct, IDispatchDataLoader
            {
                throw new NotImplementedException();
            }

            public void LoadDispatchMetadata<TLoader>(ref TLoader loader, out IntPtr result) where TLoader : struct, IDispatchMetadataLoader
            {
                throw new NotImplementedException();
            }
        }

        private List<TrigonometryFunctionTask> SineQueue = new List<TrigonometryFunctionTask>();
        private List<TrigonometryFunctionTask> CoSineQueue = new List<TrigonometryFunctionTask>();
        public GPUMath()
        {
            // check sin buffer
            Task.Run(delegate ()
            {
                while (true)
                {
                    if (SineQueue.Count >= 1024)
                    {
                        float[] inputs = new float[1024];
                        int i = 0;
                        foreach (TrigonometryFunctionTask t in SineQueue)
                        {
                            if (i > 1024)
                            {
                                break;
                            }
                            inputs[i] = t.Input;
                            i++;
                        }
                        float[] results = new float[1024];
                        var device = GraphicsDevice.GetDefault();
                        var inputBuffer = device.AllocateReadWriteBuffer(inputs);
                        var outputBuffer = device.AllocateReadWriteBuffer<float>(1024);
                        device.For(1024, new SinShader(outputBuffer, inputBuffer));
                        outputBuffer.CopyTo(results);
                        int j = 0;
                        foreach (float r in results)
                        {
                            SineQueue[j].Result = r;
                            SineQueue[j].IsSolved = true;
                            j++;
                        }
                    }
                    Thread.Sleep(1);
                }
            });
            // check cosin buffer
            Task.Run(delegate ()
            {
                while (true)
                {
                    if (CoSineQueue.Count >= 1024)
                    {
                        float[] inputs = new float[1024];
                        int i = 0;
                        foreach (TrigonometryFunctionTask t in CoSineQueue)
                        {
                            if (i > 1024)
                            {
                                break;
                            }
                            inputs[i] = t.Input;
                            i++;
                        }
                        float[] results = new float[1024];
                        var device = GraphicsDevice.GetDefault();
                        var inputBuffer = device.AllocateReadWriteBuffer(inputs);
                        var outputBuffer = device.AllocateReadWriteBuffer<float>(1024);
                        device.For(1024, new CoSinShader(outputBuffer, inputBuffer));
                        outputBuffer.CopyTo(results);
                        int j = 0;
                        foreach (float r in results)
                        {
                            CoSineQueue[j].Result = r;
                            CoSineQueue[j].IsSolved = true;
                            j++;
                        }
                        Console.WriteLine("calculated sine");
                    }
                    Thread.Sleep(1);
                }
            });
        }
        public float Sin(float x)
        {
            Console.WriteLine("sin");
            TrigonometryFunctionTask t = new TrigonometryFunctionTask(x);
            SineQueue.Add(t);
            while (!t.IsSolved) { }
            float r = t.Result;
            SineQueue.Remove(t);
            Console.WriteLine($"{r} = sin( {x} )");
            return r;
        }
        public float Cos(float x)
        {
            Console.WriteLine($"cos");
            TrigonometryFunctionTask t = new TrigonometryFunctionTask(x);
            CoSineQueue.Add(t);
            while (!t.IsSolved) { }
            float r = t.Result;
            SineQueue.Remove(t);
            Console.WriteLine($"{r} = cos( {x} )");
            return r;
        }
    }
}
