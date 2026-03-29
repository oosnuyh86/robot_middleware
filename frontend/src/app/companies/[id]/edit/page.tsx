"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { get, put } from "@/lib/api";
import type { Company } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";

export default function EditCompanyPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [name, setName] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    get<Company>(`/companies/${id}`)
      .then((c) => setName(c.name))
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load company"),
      )
      .finally(() => setLoading(false));
  }, [id]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    try {
      await put<Company>(`/companies/${id}`, { name });
      router.push(`/companies/${id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update company");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="page-container">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  return (
    <div className="page-container">
      <h1>Edit Company</h1>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <FormField
            label="Company Name"
            name="name"
            type="text"
            value={name}
            onChange={(e) => setName((e.target as HTMLInputElement).value)}
            required
          />

          <div className="form-actions">
            <Button type="submit" loading={saving}>
              Save Changes
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => router.push(`/companies/${id}`)}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
