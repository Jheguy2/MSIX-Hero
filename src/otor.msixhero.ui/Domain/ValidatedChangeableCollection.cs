﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace otor.msixhero.ui.Domain
{
    public class ValidatedChangeableCollection<T> : ChangeableCollection<T>, IValidatedContainerChangeable
    {
        private string validationMessage;
        private bool isValidated = true;
        private IReadOnlyCollection<Func<IEnumerable<T>, string>> validators;
        private ValidationMode validationMode;

        public ValidatedChangeableCollection(Func<IEnumerable<T>, string> validator = null)
        {
            if (validator != null)
            {
                this.validators = new List<Func<IEnumerable<T>, string>> { validator };
            }
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            this.Validate();
        }

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            this.Validate();
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            base.MoveItem(oldIndex, newIndex);
            this.Validate();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            this.Validate();
        }

        protected override void SetItem(int index, T item)
        {
            base.SetItem(index, item);
            this.Validate();
        }

        public string ValidationMessage
        {
            get => this.validationMessage;
            private set
            {
                var oldIsValid = string.IsNullOrEmpty(this.validationMessage);
                if (!this.SetField(ref this.validationMessage, value))
                {
                    return;
                }

                var newIsValid = string.IsNullOrEmpty(value);
                if (oldIsValid != newIsValid)
                {
                    this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(this.IsValid)));
                }
            }
        }

        public bool IsValidated
        {
            get => this.isValidated;
            set
            {
                if (!this.SetField(ref this.isValidated, value))
                {
                    return;
                }

                this.Validate();
            }
        }

        public bool IsValid => string.IsNullOrEmpty(this.validationMessage);

        public ValidationMode GetValidationMode()
        {
            return this.validationMode;
        }

        public ValidationMode ValidationMode
        {
            get => this.validationMode;
            set => this.SetField(ref this.validationMode, value);
        }

        public void SetValidationMode(ValidationMode mode, bool setForChildren)
        {
            if (setForChildren)
            {
                foreach (var item in this.OfType<IValidatedChangeable>())
                {
                    if (item is IValidatedContainerChangeable container)
                    {
                        container.SetValidationMode(mode, true);
                    }
                    else
                    {
                        item.ValidationMode = mode;
                    }
                }
            }

            this.ValidationMode = mode;
        }

        public IReadOnlyCollection<Func<IEnumerable<T>, string>> Validators
        {
            get => this.validators;
            set
            {
                this.validators = value;
                this.Validate();
            }
        }

        public event EventHandler<ValueChangedEventArgs<string>> ValidationStatusChanged;

        private void Validate()
        {
            var oldValidationMessage = this.ValidationMessage;
            if (!this.IsValidated || this.Validators == null || !this.Validators.Any())
            {
                this.ValidationMessage = null;
            }
            else
            {
                string msg = null;
                foreach (var validator in this.Validators)
                {
                    msg = validator(this);
                    if (!string.IsNullOrEmpty(msg))
                    {
                        break;
                    }
                }

                this.ValidationMessage = msg;
            }

            // ReSharper disable once InvertIf
            if (oldValidationMessage != this.ValidationMessage)
            {
                var validationChanged = this.ValidationStatusChanged;
                if (validationChanged != null)
                {
                    validationChanged(this, new ValueChangedEventArgs<string>(this.ValidationMessage));
                }
            }
        }
    }
}