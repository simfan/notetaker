import React, { useEffect, useState } from 'react';
import {
  View, Text, TextInput, TouchableOpacity, StyleSheet,
  ScrollView, Image, Alert, ActivityIndicator, KeyboardAvoidingView, Platform,
} from 'react-native';
import { useNavigation, useRoute, RouteProp } from '@react-navigation/native';
import * as ImagePicker from 'expo-image-picker';
import { Ionicons } from '@expo/vector-icons';
import { Note, Tag, NoteType, RootStackParamList } from '../types';
import { fetchNotes, fetchTags, createNote, updateNote, uploadPhoto } from '../services/notesService';
import DateTimePicker from '@react-native-community/datetimepicker';

type EditorRoute = RouteProp<RootStackParamList, 'NoteEditor'>;

const TYPE_OPTIONS: { value: NoteType; label: string; icon: string }[] = [
  { value: 'note',       label: 'Note',       icon: 'document-text-outline' },
  { value: 'task',       label: 'Task',        icon: 'checkbox-outline' },
  { value: 'assignment', label: 'Assignment',  icon: 'calendar-outline' },
];

export default function NoteEditorScreen() {
  const navigation = useNavigation<any>();
  const route = useRoute<EditorRoute>();
  const noteId = route.params?.noteId;

  const [title, setTitle]               = useState('');
  const [body, setBody]                 = useState('');
  const [photoUri, setPhotoUri]         = useState<string | null>(null);
  const [photoUrl, setPhotoUrl]         = useState<string | null>(null);
  const [allTags, setAllTags]           = useState<Tag[]>([]);
  const [selectedTagIds, setSelectedTagIds] = useState<string[]>([]);
  const [noteType, setNoteType]         = useState<NoteType>('note');
  const [dueDate, setDueDate]           = useState<Date | null>(null);
  const [showDatePicker, setShowDatePicker] = useState(false);
  const [saving, setSaving]             = useState(false);
  const [uploading, setUploading]       = useState(false);

  useEffect(() => {
    (async () => {
      const tags = await fetchTags();
      setAllTags(tags);

      if (noteId) {
        const notes = await fetchNotes();
        const note = notes.find(n => n.id === noteId);
        if (note) {
          setTitle(note.title);
          setBody(note.body ?? '');
          setPhotoUrl(note.photo_url);
          setSelectedTagIds(note.tags?.map(t => t.id) ?? []);
          setNoteType(note.note_type ?? 'note');
          if (note.note_metadata?.due_date) {
            setDueDate(new Date(note.note_metadata.due_date));
          }
        }
      }
    })();
  }, [noteId]);

  const pickImage = async () => {
    const { status } = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (status !== 'granted') {
      Alert.alert('Permission needed', 'Camera roll access is required.');
      return;
    }
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      allowsEditing: true,
      quality: 0.8,
    });
    if (!result.canceled) setPhotoUri(result.assets[0].uri);
  };

  const takePhoto = async () => {
    const { status } = await ImagePicker.requestCameraPermissionsAsync();
    if (status !== 'granted') {
      Alert.alert('Permission needed', 'Camera access is required.');
      return;
    }
    const result = await ImagePicker.launchCameraAsync({
      allowsEditing: true,
      quality: 0.8,
    });
    if (!result.canceled) setPhotoUri(result.assets[0].uri);
  };

  const toggleTag = (id: string) => {
    setSelectedTagIds(prev =>
      prev.includes(id) ? prev.filter(t => t !== id) : [...prev, id]
    );
  };

  const handleSave = async () => {
    if (!title.trim()) {
      Alert.alert('Title required', 'Please add a title for your note.');
      return;
    }
    setSaving(true);
    try {
      let finalPhotoUrl = photoUrl;
      if (photoUri) {
        setUploading(true);
        finalPhotoUrl = await uploadPhoto(photoUri);
        setUploading(false);
      }

      const metadata: Record<string, any> = {};
      if (noteType === 'task' || noteType === 'assignment') {
        metadata.is_complete = false;
      }
      if (noteType === 'assignment' && dueDate) {
        metadata.due_date = dueDate.toISOString().split('T')[0];
      }

      const payload: Partial<Note> = {
        title: title.trim(),
        body: body.trim() || null,
        photo_url: finalPhotoUrl,
        note_type: noteType,
        note_metadata: metadata,
      };

      if (noteId) {
        await updateNote(noteId, payload, selectedTagIds);
      } else {
        await createNote(payload, selectedTagIds);
      }
      navigation.goBack();
    } catch (e: any) {
      Alert.alert('Save failed', e.message);
    } finally {
      setSaving(false);
      setUploading(false);
    }
  };

  const displayPhoto = photoUri || photoUrl;

  const formatDate = (date: Date) =>
    date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

  return (
    <KeyboardAvoidingView
      style={{ flex: 1 }}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView style={styles.container} keyboardShouldPersistTaps="handled">

        {/* Nav Bar */}
        <View style={styles.navbar}>
          <TouchableOpacity onPress={() => navigation.goBack()} style={styles.navBtn}>
            <Ionicons name="chevron-back" size={24} color="#6366f1" />
          </TouchableOpacity>
          <Text style={styles.navTitle}>{noteId ? 'Edit Note' : 'New Note'}</Text>
          <TouchableOpacity onPress={handleSave} style={styles.saveBtn} disabled={saving}>
            {saving ? (
              <ActivityIndicator size="small" color="#fff" />
            ) : (
              <Text style={styles.saveBtnText}>Save</Text>
            )}
          </TouchableOpacity>
        </View>

        <View style={styles.form}>

          {/* Type Picker */}
          <Text style={styles.sectionLabel}>Type</Text>
          <View style={styles.typePicker}>
            {TYPE_OPTIONS.map(opt => {
              const active = noteType === opt.value;
              return (
                <TouchableOpacity
                  key={opt.value}
                  style={[styles.typeBtn, active && styles.typeBtnActive]}
                  onPress={() => setNoteType(opt.value)}
                >
                  <Ionicons
                    name={opt.icon as any}
                    size={18}
                    color={active ? '#fff' : '#6366f1'}
                  />
                  <Text style={[styles.typeBtnText, active && styles.typeBtnTextActive]}>
                    {opt.label}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </View>

          {/* Due Date — Assignment only */}
          {noteType === 'assignment' && (
            <View style={styles.dueDateRow}>
              <Ionicons name="calendar-outline" size={18} color="#6366f1" />
              <Text style={styles.dueDateLabel}>Due date</Text>
              <TouchableOpacity
                style={styles.dueDateBtn}
                onPress={() => setShowDatePicker(true)}
              >
                <Text style={styles.dueDateBtnText}>
                  {dueDate ? formatDate(dueDate) : 'Set date'}
                </Text>
              </TouchableOpacity>
              {dueDate && (
                <TouchableOpacity onPress={() => setDueDate(null)} style={{ padding: 4 }}>
                  <Ionicons name="close-circle" size={18} color="#94a3b8" />
                </TouchableOpacity>
              )}
            </View>
          )}

          {showDatePicker && (
            <DateTimePicker
              value={dueDate ?? new Date()}
              mode="date"
              display={Platform.OS === 'ios' ? 'inline' : 'default'}
              onChange={(_, selected) => {
                setShowDatePicker(Platform.OS === 'ios');
                if (selected) setDueDate(selected);
              }}
            />
          )}

          {/* Title */}
          <TextInput
            style={styles.titleInput}
            placeholder="Note title…"
            placeholderTextColor="#94a3b8"
            value={title}
            onChangeText={setTitle}
            maxLength={120}
          />

          {/* Body */}
          <TextInput
            style={styles.bodyInput}
            placeholder="Write your note…"
            placeholderTextColor="#94a3b8"
            value={body}
            onChangeText={setBody}
            multiline
            textAlignVertical="top"
          />

          {/* Photo Section */}
          <Text style={styles.sectionLabel}>Photo</Text>
          {displayPhoto ? (
            <View style={styles.photoContainer}>
              <Image source={{ uri: displayPhoto }} style={styles.photo} />
              <TouchableOpacity
                style={styles.removePhoto}
                onPress={() => { setPhotoUri(null); setPhotoUrl(null); }}
              >
                <Ionicons name="close-circle" size={28} color="#ef4444" />
              </TouchableOpacity>
              {uploading && (
                <View style={styles.uploadOverlay}>
                  <ActivityIndicator size="large" color="#fff" />
                  <Text style={styles.uploadText}>Uploading…</Text>
                </View>
              )}
            </View>
          ) : (
            <View style={styles.photoButtons}>
              <TouchableOpacity style={styles.photoBtn} onPress={takePhoto}>
                <Ionicons name="camera-outline" size={22} color="#6366f1" />
                <Text style={styles.photoBtnText}>Camera</Text>
              </TouchableOpacity>
              <TouchableOpacity style={styles.photoBtn} onPress={pickImage}>
                <Ionicons name="image-outline" size={22} color="#6366f1" />
                <Text style={styles.photoBtnText}>Gallery</Text>
              </TouchableOpacity>
            </View>
          )}

          {/* Tags Section */}
          <Text style={styles.sectionLabel}>Tags</Text>
          {allTags.length === 0 ? (
            <Text style={styles.noTagsText}>
              No tags yet. Create tags in the Tag Manager.
            </Text>
          ) : (
            <View style={styles.tagGrid}>
              {allTags.map(tag => {
                const active = selectedTagIds.includes(tag.id);
                return (
                  <TouchableOpacity
                    key={tag.id}
                    onPress={() => toggleTag(tag.id)}
                    style={[
                      styles.tagChip,
                      { borderColor: tag.color },
                      active && { backgroundColor: tag.color },
                    ]}
                  >
                    <Text style={[styles.tagChipText, { color: active ? '#fff' : tag.color }]}>
                      {tag.name}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          )}
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f8fafc' },
  navbar: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
    paddingTop: 60, paddingBottom: 12, paddingHorizontal: 16,
    backgroundColor: '#fff', borderBottomWidth: 1, borderBottomColor: '#f1f5f9',
  },
  navBtn: { padding: 4 },
  navTitle: { fontSize: 17, fontWeight: '600', color: '#1e293b' },
  saveBtn: {
    backgroundColor: '#6366f1', paddingHorizontal: 18, paddingVertical: 8,
    borderRadius: 10, minWidth: 60, alignItems: 'center',
  },
  saveBtnText: { color: '#fff', fontWeight: '600', fontSize: 15 },
  form: { padding: 20 },

  // Type Picker
  typePicker: {
    flexDirection: 'row', gap: 10, marginBottom: 20,
  },
  typeBtn: {
    flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    gap: 6, paddingVertical: 10, borderRadius: 10,
    borderWidth: 1.5, borderColor: '#6366f1', backgroundColor: '#fff',
  },
  typeBtnActive: {
    backgroundColor: '#6366f1', borderColor: '#6366f1',
  },
  typeBtnText: { fontSize: 13, fontWeight: '600', color: '#6366f1' },
  typeBtnTextActive: { color: '#fff' },

  // Due Date
  dueDateRow: {
    flexDirection: 'row', alignItems: 'center', gap: 8,
    backgroundColor: '#fff', borderRadius: 12, padding: 14,
    marginBottom: 20, borderWidth: 1, borderColor: '#e2e8f0',
  },
  dueDateLabel: { fontSize: 14, fontWeight: '600', color: '#475569', flex: 1 },
  dueDateBtn: {
    backgroundColor: '#f1f5f9', paddingHorizontal: 12, paddingVertical: 6,
    borderRadius: 8,
  },
  dueDateBtnText: { fontSize: 14, fontWeight: '600', color: '#6366f1' },

  titleInput: {
    fontSize: 22, fontWeight: '700', color: '#1e293b',
    borderBottomWidth: 1, borderBottomColor: '#e2e8f0',
    paddingBottom: 12, marginBottom: 16,
  },
  bodyInput: {
    fontSize: 16, color: '#334155', lineHeight: 24,
    minHeight: 160, backgroundColor: '#fff', borderRadius: 12,
    padding: 14, marginBottom: 24,
    shadowColor: '#000', shadowOpacity: 0.04, shadowRadius: 4, elevation: 1,
  },
  sectionLabel: {
    fontSize: 13, fontWeight: '700', color: '#94a3b8',
    textTransform: 'uppercase', letterSpacing: 1, marginBottom: 12,
  },
  photoContainer: { borderRadius: 12, overflow: 'hidden', marginBottom: 24 },
  photo: { width: '100%', height: 220, resizeMode: 'cover' },
  removePhoto: { position: 'absolute', top: 8, right: 8 },
  uploadOverlay: {
    ...StyleSheet.absoluteFillObject, backgroundColor: 'rgba(0,0,0,0.5)',
    alignItems: 'center', justifyContent: 'center',
  },
  uploadText: { color: '#fff', marginTop: 8, fontSize: 15 },
  photoButtons: { flexDirection: 'row', gap: 12, marginBottom: 24 },
  photoBtn: {
    flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center',
    gap: 8, paddingVertical: 14, borderRadius: 12,
    backgroundColor: '#fff', borderWidth: 1.5, borderColor: '#e2e8f0',
    shadowColor: '#000', shadowOpacity: 0.04, shadowRadius: 4, elevation: 1,
  },
  photoBtnText: { fontSize: 15, fontWeight: '600', color: '#6366f1' },
  tagGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10, marginBottom: 40 },
  tagChip: {
    paddingHorizontal: 14, paddingVertical: 8,
    borderRadius: 20, borderWidth: 1.5,
  },
  tagChipText: { fontSize: 14, fontWeight: '600' },
  noTagsText: { color: '#94a3b8', fontSize: 14, marginBottom: 24 },
});
