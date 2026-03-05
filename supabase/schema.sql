-- ============================================================
-- NoteKeeper — Supabase Schema
-- ============================================================

-- Enable UUID generation
create extension if not exists "uuid-ossp";

-- ------------------------------------------------------------
-- TABLES
-- ------------------------------------------------------------

create table public.groups (
  id          uuid primary key default uuid_generate_v4(),
  user_id     uuid references auth.users(id) on delete cascade not null,
  name        text not null,
  color       text default '#6366f1',
  created_at  timestamptz default now()
);

create table public.tags (
  id          uuid primary key default uuid_generate_v4(),
  user_id     uuid references auth.users(id) on delete cascade not null,
  name        text not null,
  color       text default '#10b981',
  created_at  timestamptz default now(),
  unique(user_id, name)
);

create table public.notes (
  id          uuid primary key default uuid_generate_v4(),
  user_id     uuid references auth.users(id) on delete cascade not null,
  group_id    uuid references public.groups(id) on delete set null,
  title       text not null default 'Untitled Note',
  body        text,
  photo_url   text,
  created_at  timestamptz default now(),
  updated_at  timestamptz default now()
);

create table public.note_tags (
  note_id     uuid references public.notes(id) on delete cascade,
  tag_id      uuid references public.tags(id) on delete cascade,
  primary key (note_id, tag_id)
);

-- ------------------------------------------------------------
-- STORAGE BUCKET
-- ------------------------------------------------------------

insert into storage.buckets (id, name, public)
values ('note-photos', 'note-photos', true);

-- ------------------------------------------------------------
-- ROW LEVEL SECURITY
-- ------------------------------------------------------------

alter table public.groups   enable row level security;
alter table public.tags     enable row level security;
alter table public.notes    enable row level security;
alter table public.note_tags enable row level security;

-- Groups
create policy "Users manage own groups"   on public.groups   for all using (auth.uid() = user_id);
create policy "Users manage own tags"     on public.tags     for all using (auth.uid() = user_id);
create policy "Users manage own notes"    on public.notes    for all using (auth.uid() = user_id);
create policy "Users manage own note_tags" on public.note_tags for all
  using (exists (select 1 from public.notes n where n.id = note_id and n.user_id = auth.uid()));

-- Storage
create policy "Users upload own photos" on storage.objects for insert
  with check (bucket_id = 'note-photos' and auth.uid()::text = (storage.foldername(name))[1]);
create policy "Public read photos" on storage.objects for select
  using (bucket_id = 'note-photos');

-- ------------------------------------------------------------
-- UPDATED_AT TRIGGER
-- ------------------------------------------------------------

create or replace function public.handle_updated_at()
returns trigger language plpgsql as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create trigger notes_updated_at before update on public.notes
  for each row execute function public.handle_updated_at();
