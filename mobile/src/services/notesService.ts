import { supabase } from './supabase';
import * as FileSystem from 'expo-file-system/legacy';
import { decode } from 'base64-arraybuffer';
import { Note, Tag } from '../types';

// ── Notes ──────────────────────────────────────────────────

export async function fetchNotes(): Promise<Note[]> {
  const { data, error } = await supabase
    .from('notes')
    .select(`*, group:groups(*), tags:note_tags(tag:tags(*))`)
    .order('updated_at', { ascending: false });

  if (error) throw error;

  return (data ?? []).map((n: any) => ({
    ...n,
    tags: n.tags?.map((nt: any) => nt.tag).filter(Boolean) ?? [],
  }));
}

export async function createNote(note: Partial<Note>, tagIds: string[] = []): Promise<Note> {
  const user = (await supabase.auth.getUser()).data.user;
  const userId = user?.id ?? '00000000-0000-0000-0000-000000000000';

  const { data, error } = await supabase
    .from('notes')
    .insert({ ...note, user_id: userId })
    .select()
    .single();

  if (error) throw error;

  if (tagIds.length > 0) {
    await supabase.from('note_tags').insert(tagIds.map(tid => ({ note_id: data.id, tag_id: tid })));
  }

  return data;
}

export async function updateNote(id: string, updates: Partial<Note>, tagIds?: string[]): Promise<Note> {
  const { data, error } = await supabase
    .from('notes')
    .update(updates)
    .eq('id', id)
    .select()
    .single();

  if (error) throw error;

  if (tagIds !== undefined) {
    await supabase.from('note_tags').delete().eq('note_id', id);
    if (tagIds.length > 0) {
      await supabase.from('note_tags').insert(tagIds.map(tid => ({ note_id: id, tag_id: tid })));
    }
  }

  return data;
}

export async function deleteNote(id: string): Promise<void> {
  const { error } = await supabase.from('notes').delete().eq('id', id);
  if (error) throw error;
}

// ── Photo Upload ───────────────────────────────────────────

export async function uploadPhoto(localUri: string): Promise<string> {
  const user = (await supabase.auth.getUser()).data.user;
  const userId = user?.id ?? '00000000-0000-0000-0000-000000000000';

  const base64 = await FileSystem.readAsStringAsync(localUri, {
    encoding: 'base64' as any,
  });

  const ext = localUri.split('.').pop() ?? 'jpg';
  const path = `${userId}/${Date.now()}.${ext}`;

  const { error } = await supabase.storage
    .from('note-photos')
    .upload(path, decode(base64), { contentType: `image/${ext}` });

  if (error) throw error;

  const { data } = supabase.storage.from('note-photos').getPublicUrl(path);
  return data.publicUrl;
}

// ── Tags ───────────────────────────────────────────────────

export async function fetchTags(): Promise<Tag[]> {
  const { data, error } = await supabase.from('tags').select('*').order('name');
  if (error) throw error;
  return data ?? [];
}

export async function createTag(name: string, color: string): Promise<Tag> {
  const user = (await supabase.auth.getUser()).data.user;
  const userId = user?.id ?? '00000000-0000-0000-0000-000000000000';

  const { data, error } = await supabase
    .from('tags')
    .insert({ name, color, user_id: userId })
    .select()
    .single();

  if (error) throw error;
  return data;
}

export async function deleteTag(id: string): Promise<void> {
  const { error } = await supabase.from('tags').delete().eq('id', id);
  if (error) throw error;
}