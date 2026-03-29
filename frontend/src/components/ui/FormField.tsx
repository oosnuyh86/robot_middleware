"use client";

import { type InputHTMLAttributes, type TextareaHTMLAttributes } from "react";

interface FormFieldBaseProps {
  label: string;
  error?: string;
  required?: boolean;
}

type InputFieldProps = FormFieldBaseProps &
  InputHTMLAttributes<HTMLInputElement> & { multiline?: false };

type TextareaFieldProps = FormFieldBaseProps &
  TextareaHTMLAttributes<HTMLTextAreaElement> & { multiline: true };

type FormFieldProps = InputFieldProps | TextareaFieldProps;

export function FormField(props: FormFieldProps) {
  const { label, error, required, multiline, ...rest } = props;
  const id = rest.id || rest.name || label.toLowerCase().replace(/\s+/g, "-");

  return (
    <div className="form-field">
      <label htmlFor={id}>
        {label}
        {required && <span className="required"> *</span>}
      </label>
      {multiline ? (
        <textarea
          id={id}
          className={error ? "input-error" : ""}
          {...(rest as TextareaHTMLAttributes<HTMLTextAreaElement>)}
        />
      ) : (
        <input
          id={id}
          className={error ? "input-error" : ""}
          {...(rest as InputHTMLAttributes<HTMLInputElement>)}
        />
      )}
      {error && <span className="field-error">{error}</span>}
    </div>
  );
}
