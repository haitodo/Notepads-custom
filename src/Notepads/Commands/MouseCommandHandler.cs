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

    public sealed class MouseCommandHandler : ICommandHandler<PointerRoutedEventArgs>
    {
        public readonly ICollection<IMouseCommand<PointerRoutedEventArgs>> Commands;

        private readonly UIElement _relativeTo;

        public MouseCommandHandler(ICollection<IMouseCommand<PointerRoutedEventArgs>> commands, UIElement relativeTo)
        {
            Commands = commands;
            _relativeTo = relativeTo;
        }

        public CommandHandlerResult Handle(PointerRoutedEventArgs args)
        {
            var ctrlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var altDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var shiftDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var point = args.GetCurrentPoint(_relativeTo).Properties;
            var shouldHandle = false;
            var shouldSwallow = false;

            foreach (var command in Commands)
            {
                if (command.Hit(
                    ctrlDown,
                    altDown,
                    shiftDown,
                    point.IsLeftButtonPressed,
                    point.IsMiddleButtonPressed,
                    point.IsRightButtonPressed))
                {
                    command.Execute(args);

                    if (command.ShouldSwallowAfterExecution())
                    {
                        shouldSwallow = true;
                    }

                    if (command.ShouldHandleAfterExecution())
                    {
                        shouldHandle = true;
                    }

                    break;
                }
            }

            return new CommandHandlerResult(shouldHandle, shouldSwallow);
        }
    }
}