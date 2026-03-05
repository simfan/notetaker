export interface Note {
  id: string;
  user_id: string;
  group_id: string | null;
  title: string;
  body: string | null;
  photo_url: string | null;
  created_at: string;
  updated_at: string;
  tags?: Tag[];
  group?: Group;
}

export interface Tag {
  id: string;
  user_id: string;
  name: string;
  color: string;
  created_at: string;
}

export interface Group {
  id: string;
  user_id: string;
  name: string;
  color: string;
  created_at: string;
}

export interface NoteTag {
  note_id: string;
  tag_id: string;
}

export type RootStackParamList = {
  NoteList: undefined;
  NoteEditor: { noteId?: string };
  TagManager: undefined;
  Settings: undefined;
};
