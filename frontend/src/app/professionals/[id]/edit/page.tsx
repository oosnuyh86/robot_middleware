"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { get, put } from "@/lib/api";
import type { Professional, Company } from "@/types/models";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";

export default function EditProfessionalPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;

  const [name, setName] = useState("");
  const [profileSummary, setProfileSummary] = useState("");
  const [companyId, setCompanyId] = useState("");
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([
      get<Professional>(`/professionals/${id}`),
      get<Company[]>("/companies"),
    ])
      .then(([prof, comps]) => {
        setName(prof.name);
        setProfileSummary(prof.profile_summary);
        setCompanyId(prof.company_id);
        setCompanies(comps);
      })
      .catch((err) =>
        setError(err instanceof Error ? err.message : "Failed to load data"),
      )
      .finally(() => setLoading(false));
  }, [id]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError(null);

    try {
      await put<Professional>(`/professionals/${id}`, {
        name,
        profile_summary: profileSummary,
        company_id: companyId,
      });
      router.push(`/professionals/${id}`);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to update professional",
      );
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
      <h1>Edit Professional</h1>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <FormField
            label="Name"
            name="name"
            type="text"
            value={name}
            onChange={(e) => setName((e.target as HTMLInputElement).value)}
            required
          />

          <FormField
            label="Profile Summary"
            name="profile_summary"
            multiline
            value={profileSummary}
            onChange={(e) =>
              setProfileSummary((e.target as HTMLTextAreaElement).value)
            }
          />

          <Select
            label="Company"
            name="company_id"
            value={companyId}
            onChange={(e) => setCompanyId(e.target.value)}
            options={companies.map((c) => ({ value: c._id, label: c.name }))}
            required
          />

          <div className="form-actions">
            <Button type="submit" loading={saving}>
              Save Changes
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => router.push(`/professionals/${id}`)}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
