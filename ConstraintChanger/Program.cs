﻿#region License and Terms
// Unconstrained Melody
// Copyright (c) 2009-2011 Jonathan Skeet. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ConstraintChanger
{
    class Program
    {
        const string InputAssembly = "UnconstrainedMelody.dll";
        const string OutputAssembly = InputAssembly;
        const string OutputDirectory = @"../../../Rewritten";

        private static readonly string[] SdkPaths =
        {
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7 Tools",
            @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.2 Tools",
        };

        static int Main()
        {
            string ildasmExe = FindIldasm();
            if (ildasmExe == null)
            {
                // Error message will already have been written
                return 1;
            }
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string framework4Directory = Path.Combine(windows, @"..\Microsoft.NET\Framework\v4.0.30319");
            string framework2Directory = Path.Combine(windows, @"..\Microsoft.NET\Framework\v2.0.50727");
            string ilasmExe = Path.Combine(framework4Directory, "ilasm.exe");
            if (!File.Exists(ilasmExe))
            {
                ilasmExe = Path.Combine(framework2Directory, "ilasm.exe"); ;
                if (!File.Exists(ilasmExe))
                {
                    Console.WriteLine("Can't find ilasm. Aborting. Expected it at: {0}", ilasmExe);
                    return 1;
                }
            }

            try
            {
                string ilFile = Decompile(ildasmExe);
                ChangeConstraints(ilFile);
                Recompile(ilFile, ilasmExe);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}\r\n{1}", e.Message, e.StackTrace);
                return 1;
            }
            return 0;
        }

        private static string FindIldasm()
        {
            //string[] programFiles = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            //                                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            //foreach (string root in programFiles)
            //{
            //    foreach (string sdkPath in SdkPaths)
            //    {
            //        string directory = Path.Combine(root, sdkPath);
            //        if (Directory.Exists(directory))
            //        {
            //            string ildasm = Path.Combine(directory, "ildasm.exe");
            //            if (File.Exists(ildasm))
            //            {
            //                return ildasm;
            //            }
            //        }
            //    }
            //}
            foreach (string sdkPath in SdkPaths)
            {
                if (Directory.Exists(sdkPath))
                {
                    string ildasm = Path.Combine(sdkPath, "ildasm.exe");
                    if (File.Exists(ildasm))
                    {
                        return ildasm;
                    }
                }
            }
            Console.WriteLine("Unable to find SDK directory containing ildasm.exe. Aborting.");
            return null;
        }

        private static string Decompile(string ildasmExe)
        {
            string ilFile = Path.GetTempFileName();
            Console.WriteLine("Decompiling to {0}", ilFile);
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = ildasmExe,
                Arguments = "\"/OUT=" + ilFile + "\" " + InputAssembly,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception("ildasm failed");
            }
            return ilFile;
        }

        private static void Recompile(string ilFile, string ilasmExe)
        {
            if (!Directory.Exists(OutputDirectory))
            {
                Console.WriteLine("Creating output directory");
                Directory.CreateDirectory(OutputDirectory);
            }

            string resFile = Path.ChangeExtension(ilFile, ".res");

            string output = Path.Combine(OutputDirectory, OutputAssembly);
            Console.WriteLine("Recompiling {0} to {1}", ilFile, output);
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = ilasmExe,
                Arguments = "/OUTPUT=" + output + " /DLL " + "\"" + ilFile + "\" /RESOURCE=\"" + resFile + "\"",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process.WaitForExit();
        }

        private static void ChangeConstraints(string ilFile)
        {
            string[] lines = File.ReadAllLines(ilFile);
            lines = lines.Select<string, string>(ChangeLine).ToArray();
            File.WriteAllLines(ilFile, lines);
        }

        private static string ChangeLine(string line)
        {
            // Surely this is too simple to actually work...
            return line.Replace("(UnconstrainedMelody.DelegateConstraint)", "([mscorlib]System.Delegate)")
                       .Replace("([mscorlib]System.ValueType, UnconstrainedMelody.IEnumConstraint)", "([mscorlib]System.Enum)")
                       // Roslyn puts the constrains in the opposite order...
                       .Replace("(UnconstrainedMelody.IEnumConstraint, [mscorlib]System.ValueType)", "([mscorlib]System.Enum)");
        }
    }
}
