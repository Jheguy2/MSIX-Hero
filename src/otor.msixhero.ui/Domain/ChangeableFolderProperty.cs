﻿using System;
using System.IO;
using System.Windows.Input;
using Otor.MsixHero.Infrastructure.Services;
using Otor.MsixHero.Ui.Commands.RoutedCommand;

namespace Otor.MsixHero.Ui.Domain
{
    public class ChangeableFolderProperty : ValidatedChangeableProperty<string>
    {
        // ReSharper disable once InconsistentNaming
        private static Func<string, string> validatePath;

        // ReSharper disable once InconsistentNaming
        private static Func<string, string> validatePathAndPresence;

        private readonly IInteractionService interactionService;
        private ICommand browse;

        public ChangeableFolderProperty(IInteractionService interactionService, string initialFolder) : base(initialFolder)
        {
            this.interactionService = interactionService;
        }

        public ChangeableFolderProperty(IInteractionService interactionService, string initialFolder, params Func<string, string>[] validators) : base(initialFolder, validators)
        {
            this.interactionService = interactionService;
        }

        public ChangeableFolderProperty(IInteractionService interactionService, params Func<string, string>[] validators) : base(validators)
        {
            this.interactionService = interactionService;
        }

        public static Func<string, string> ValidatePath
        {
            get
            {
                return validatePath ??= value =>
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        return "The path may not be empty.";
                    }

                    if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                    {
                        return "The path is invalid.";
                    }
                    
                    return null;
                };
            }
        }

        public static Func<string, string> ValidatePathAndPresence
        {
            get
            {
                return validatePathAndPresence ??= value =>
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        return "The path may not be empty.";
                    }

                    if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                    {
                        return "The path is invalid.";
                    }


                    if (!Directory.Exists(value))
                    {
                        return "The folder does not exist.";
                    }

                    return null;
                };
            }
        }

        public ICommand Browse
        {
            get
            {
                return this.browse ??= new DelegateCommand(param =>
                {
                    if (string.IsNullOrEmpty(this.CurrentValue))
                    {
                        if (this.interactionService.SelectFolder(out var newValue))
                        {
                            this.CurrentValue = newValue;
                        }
                    }
                    else
                    {
                        if (this.interactionService.SelectFolder(this.CurrentValue, out var newValue))
                        {
                            this.CurrentValue = newValue;
                        }
                    }
                });
            }
        }
    }
}