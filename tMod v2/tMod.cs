﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using LuaInterface;
using System.Threading;
using Terraria;

namespace tMod_v3
{
    public class tMod
    {
        public static Type worldGen;
        public static Type main;
        AssemblyDefinition terrariaAsm;
        ModuleDefinition module;
        ModuleDefinition tMod2;
        MemoryStream stream = new MemoryStream();

        public void writeLine(string write)
        {
            Console.WriteLine(write);
        }

        public void inject()
        {
            Load();

            module.Types.Remove(module.Types["Terraria.ProgramServer"]);
            module.Inject(tMod2.Types["Terraria.Program"]);
            terrariaAsm.EntryPoint = getMethod(module.Types["Terraria.Program"], "tMod");

            modifyMain();
            ModifyMessageBuffer();
            ModifyNetMessage();
            ModifyNetplay();

            Write();

            Assembly asm = System.Reflection.Assembly.Load(stream.GetBuffer());
            worldGen = asm.GetType("Terraria.WorldGen");
            main = asm.GetType("Terraria.Main");

            ItemMod.Item = asm.GetType("Terraria.Item");
            MainMod.main = asm.GetType("Terraria.Main");
            MessageBufferMod.messageBuffer = asm.GetType("Terraria.messageBuffer");
            NetMessageMod.NetMessage = asm.GetType("Terraria.NetMessage");
            NetplayMod.Netplay = asm.GetType("Terraria.Netplay");
            NPCMod.NPC = asm.GetType("Terraria.NPC");
            WorldGenMod.WorldGen = asm.GetType("Terraria.WorldGen");

            try
            {
                asm.EntryPoint.Invoke(null, new object[] { new string[0] });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine("Program crashed...");
            }
            
            Console.ReadKey();
        }

        private void Write()
        {
            AssemblyFactory.SaveAssembly(terrariaAsm, stream);
        }

        private void Load()
        {
            Console.WriteLine("Loading original Terraria assembly");
            tMod2 = AssemblyFactory.GetAssembly(System.Reflection.Assembly.GetExecutingAssembly().Location).MainModule;
            while (true)
            {
                try
                {
                    string Path = Environment.CurrentDirectory + @"\";
                    terrariaAsm = AssemblyFactory.GetAssembly(Path + "TerrariaServer.exe");
                    module = terrariaAsm.MainModule;
                    break;
                }
                catch
                {
                    Console.WriteLine("TerrariaServer.exe couldn't be found in the same directory as tModServer.exe");
                    Console.WriteLine("Please move tMod v3.exe into the same directory as the TerrariaServer.exe");
                    Console.ReadKey();
                }
            }
        }

        public void ModifyNetplay()
        {
            ExceptionHandler exh = new ExceptionHandler(ExceptionHandlerType.Catch);
            TypeReference exception = module.Import(typeof(Exception));
            VariableDefinition ex = new VariableDefinition(exception);
            MethodDefinition listenForClients = getMethod(module.Types["Terraria.Netplay"], "ListenForClients");
            CilWorker cil = listenForClients.Body.CilWorker;
            Instruction instr = listenForClients.Body.Instructions[listenForClients.Body.Instructions.Count - 1];
            listenForClients.Body.Variables.Add(ex);
            exh.TryStart = listenForClients.Body.Instructions[0];
            exh.CatchType = exception;
            cil.InsertAfter(instr, instr = exh.TryEnd = exh.HandlerStart = cil.Create(OpCodes.Stloc, ex));
            cil.InsertAfter(instr, instr = cil.Create(OpCodes.Ldloc, ex));
            cil.InsertAfter(instr, instr = cil.Create(OpCodes.Call, module.Import(typeof(Console).GetMethod("WriteLine", new Type[] { typeof(object) }))));
            cil.InsertAfter(instr, instr = exh.HandlerEnd = cil.Create(OpCodes.Ret));
            listenForClients.Body.ExceptionHandlers.Add(exh);
        }

        public void modifyMain()
        {
            Console.WriteLine("Modifying Terraria.Main");
            TypeDefinition type = module.Types["Terraria.Main"];
            MethodDefinition update = getMethod(type, "Update");
            MethodReference updateMod = module.Import(typeof(MainMod).GetMethod("UpdateMod"));
            MethodDefinition startDedInput = getMethod(type, "startDedInput");
            MethodReference startDedInputMod = module.Import(typeof(MainMod).GetMethod("StartDedInputMod"));

            CilWorker cil;

            // Call UpdateMod
            cil = update.Body.CilWorker;
            cil.InsertBefore(update.Body.Instructions[0], cil.Create(OpCodes.Call, updateMod));

            // Call StartDedInputMod
            cil = startDedInput.Body.CilWorker;
            cil.InsertBefore(startDedInput.Body.Instructions[startDedInput.Body.Instructions.Count - 1], cil.Create(OpCodes.Call, startDedInputMod));
        }

        private void ModifyNetMessage()
        {
            Console.WriteLine("Modifying Terraria.NetMessage");
            TypeDefinition type = module.Types["Terraria.NetMessage"];
            MethodDefinition greetPlayer = getMethod(type, "greetPlayer");
            MethodReference greetPlayerMod = module.Import(typeof(NetMessageMod).GetMethod("GreetPlayerMod"));
            MethodDefinition SendData = getMethod(type, "SendData");
            MethodReference SendDataTrick = module.Import(typeof(NetMessageMod).GetMethod("SendDataTrick"));

            CilWorker cil = greetPlayer.Body.CilWorker;
            Instruction instr = greetPlayer.Body.Instructions[greetPlayer.Body.Instructions.Count - 1];

            cil.InsertBefore(instr, cil.Create(OpCodes.Ldarg_0));
            cil.InsertBefore(instr, cil.Create(OpCodes.Call, greetPlayerMod));

            /*cil = SendData.Body.CilWorker;
            List<Instruction> ins = new List<Instruction>();
            foreach (Instruction str in SendData.Body.Instructions)
            {
                if (str.Operand != null && str.Operand.ToString().Contains("BeginWrite"))
                {
                    ins.Add(str);
                }
            }
            foreach (Instruction str in ins)
            {
                cil.Replace(str, cil.Create(OpCodes.Callvirt, SendDataTrick));
            }*/
        }

        private void ModifyMessageBuffer()
        {
            Console.WriteLine("Modifying Terraria.messageBuffer");
            TypeDefinition type = module.Types["Terraria.messageBuffer"];
            MethodDefinition getData = getMethod(type, "GetData");
            MethodReference getDataMod = module.Import(typeof(MessageBufferMod).GetMethod("GetDataMod"));

            CilWorker cil = getData.Body.CilWorker;
            Instruction instr = getData.Body.Instructions[0];
            cil.InsertBefore(instr, cil.Create(OpCodes.Ldarg_0));
            cil.InsertBefore(instr, cil.Create(OpCodes.Ldarg_1));
            cil.InsertBefore(instr, cil.Create(OpCodes.Ldarg_2));
            cil.InsertBefore(instr, cil.Create(OpCodes.Call, getDataMod));
            cil.InsertBefore(instr, cil.Create(OpCodes.Brfalse, getData.Body.Instructions[getData.Body.Instructions.Count - 1]));
        }

        private static MethodDefinition getMethod(TypeDefinition type, string name)
        {
            for (int i = 0; i < type.Methods.Count; i++)
            {
                if (type.Methods[i].Name == name)
                {
                    return type.Methods[i];
                }
            }
            throw new Exception("Method " + name + " does not exist.");
        }
    }
}
