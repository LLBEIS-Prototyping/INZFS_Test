﻿using OrchardCore.ContentManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace INZFS.MVC.Models.DynamicForm
{
    public class TextInputModel : BaseModel
    {
        protected override IEnumerable<ValidationResult> ExtendedValidation(ValidationContext validationContext)
        {
            if (Mandatory == true)
            {
                if (string.IsNullOrEmpty(DataInput))
                {
                    yield return new ValidationResult(ErrorMessage, new[] { nameof(DataInput) });
                }
                else
                {
                    if (CurrentPage.MaxLengthValidationType == MaxLengthValidationType.Character)
                    {
                        if (DataInput.Length > CurrentPage.MaxLength)
                        {
                            yield return new ValidationResult($"{CurrentPage.FriendlyFieldName} must be {CurrentPage.MaxLength} characters or fewer", new[] { nameof(DataInput) });
                        }
                    }
                    else
                    {
                        var numberOfWords = DataInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                        if (numberOfWords > CurrentPage.MaxLength)
                        {
                            yield return new ValidationResult($"{CurrentPage.FriendlyFieldName} must be {CurrentPage.MaxLength} words or fewer", new[] { nameof(DataInput) });
                        }
                    }
                }
            }
        }
    }

}
