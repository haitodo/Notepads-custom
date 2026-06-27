// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Commands
{
    using System.Collections.Generic;
    using Windows.System;
    using Windows.UI.Core;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;

    public sealed class KeyboardCommandHandler : ICommandHandler<KeyRoutedEventArgs>
    {
        public readonly ICollection<IKeyboardCommand<KeyRoutedEventArgs>> Commands;

        private IKeyboardCommand<KeyRoutedEventArgs> _lastCommand;

        public KeyboardCommandHandler(ICollection<IKeyboardCommand<KeyRoutedEventArgs>> commands)
        {
            Commands = commands;
        }

        public CommandHandlerResult Handle(KeyRoutedEventArgs args)
        {
            var ctrlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var altDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var shiftDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var shouldHandle = false;
            var shouldSwallow = false;

            foreach (var command in Commands)
            {
                if (command.Hit(ctrlDown, altDown, shiftDown, args.Key))
                {
                    if (command.ShouldExecute(_lastCommand))
                    {
                        command.Execute(args);
                    }

                    if (command.ShouldSwallowAfterExecution())
                    {
                        shouldSwallow = true;
                    }

                    if (command.ShouldHandleAfterExecution())
                    {
                        shouldHandle = true;
                    }

                    _lastCommand = command;
                    break;
                }
            }

            if (!shouldHandle)
            {
                _lastCommand = null;
            }

            return new CommandHandlerResult(shouldHandle, shouldSwallow);
        }
    }
}