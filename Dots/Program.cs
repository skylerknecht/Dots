﻿using Dots.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dots
{
    public class Program
    {
        private static TaskManager _taskManager;
        private static CancellationTokenSource _tokenSource;

        public static void Main(string[] args)
        {
            _taskManager = new TaskManager(args[0]);
            _taskManager.Init();
            _taskManager.InitialCheckin(args[1]);
            _taskManager.Start();
            _tokenSource = new CancellationTokenSource();
            while (!_tokenSource.IsCancellationRequested)
            {
                if(_taskManager.RetrieveBatchRequest(out var tasks))
                {
                    foreach (var task in tasks)
                    {
                        ExecuteTask(task);
                    }
                }
            }
        }

        public void Stop()
        {
            _tokenSource.Cancel();
        }

        private static void ExecuteTask(TaskRequest task)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(DotsCommand)))
                    {
                        DotsCommand command = (DotsCommand)Activator.CreateInstance(type);
                        if (command.Name == task.Method)
                        {
                            command.Execute(task);
                            if (command.Result != null)
                            {
                                _taskManager.SendResult(command.Result);
                            }
                            if (command.Error != null)
                            {
                                _taskManager.SendError(command.Error);
                            }
                            return;
                        }
                    }
                }
            }


            TaskError methodNotSupportedError = new TaskError
            {
                JSONRPC = task.JSONRPC,
                Error = new TaskErrorDetails
                {
                    Code = -32601,
                    Message = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{task.Method} is not supported")),
                },
                Id = task.Id,
            };
            _taskManager.SendError(methodNotSupportedError);
        }
    }
}
