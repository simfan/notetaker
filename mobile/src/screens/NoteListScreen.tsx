import React, { useEffect, useState, useCallback } from 'react';
import {
  View, Text, FlatList, TouchableOpacity, TextInput,
  StyleSheet, RefreshControl, Image, StatusBar, Alert,
} from 'react-native';
import { useFocusEffect, useNavigation } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';
import { Note, Tag } from '../types';
import { fetchNotes, deleteNote } from '../services/notesService';

const TAG_COLORS: Record<string, string> = {};

export default function NoteListScreen() {
  const navigation = useNavigation<any>();
  const [notes, setNotes] = useState<Note[]>([]);
  const [filtered, setFiltered] = useState<Note[]>([]);
  const [search, setSearch] = useState('');
  const [activeTag, setActiveTag] = useState<string | null>(null);
  const [allTags, setAllTags] = useState<Tag[]>([]);
  const [refreshing, setRefreshing] = useState(false);

  const load = async () => {
    try {
      const data = await fetchNotes();
      setNotes(data);
      const tags = Array.from(
        new Map(data.flatMap(n => n.tags ?? []).map(t => [t.id, t])).values()
      );
      setAllTags(tags);
    } catch (e: any) {
      Alert.alert('Error', e.message);
    }
  };

  useFocusEffect(useCallback(() => { load(); }, []));

  useEffect(() => {
    let result = notes;
    if (search.trim()) {
      const q = search.toLowerCase();
      result = result.filter(n =>
        n.title.toLowerCase().includes(q) || n.body?.toLowerCase().includes(q)
      );
    }
    if (activeTag) {
      result = result.filter(n => n.tags?.some(t => t.id === activeTag));
    }
    setFiltered(result);
  }, [notes, search, activeTag]);

  const handleDelete = (id: string) => {
    Alert.alert('Delete Note', 'Are you sure?', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Delete', style: 'destructive', onPress: async () => {
        await deleteNote(id);
        load();
      }},
    ]);
  };

  const renderNote = ({ item }: { item: Note }) => (
    <TouchableOpacity
      style={styles.card}
      onPress={() => navigation.navigate('NoteEditor', { noteId: item.id })}
      onLongPress={() => handleDelete(item.id)}
    >
      {item.photo_url && (
        <Image source={{ uri: item.photo_url }} style={styles.cardPhoto} />
      )}
      <View style={styles.cardBody}>
        <Text style={styles.cardTitle} numberOfLines={1}>{item.title}</Text>
        {item.body ? <Text style={styles.cardPreview} numberOfLines={2}>{item.body}</Text> : null}
        <View style={styles.tagRow}>
          {(item.tags ?? []).map(tag => (
            <View key={tag.id} style={[styles.tag, { backgroundColor: tag.color + '33' }]}>
              <Text style={[styles.tagText, { color: tag.color }]}>{tag.name}</Text>
            </View>
          ))}
        </View>
        <Text style={styles.cardDate}>
          {new Date(item.updated_at).toLocaleDateString()}
        </Text>
      </View>
    </TouchableOpacity>
  );

  return (
    <View style={styles.container}>
      <StatusBar barStyle="dark-content" />

      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.headerTitle}>NoteKeeper</Text>
        <TouchableOpacity onPress={() => navigation.navigate('TagManager')}>
          <Ionicons name="pricetags-outline" size={24} color="#6366f1" />
        </TouchableOpacity>
      </View>

      {/* Search */}
      <View style={styles.searchRow}>
        <Ionicons name="search" size={18} color="#94a3b8" style={{ marginRight: 8 }} />
        <TextInput
          style={styles.searchInput}
          placeholder="Search notes…"
          placeholderTextColor="#94a3b8"
          value={search}
          onChangeText={setSearch}
        />
        {search ? (
          <TouchableOpacity onPress={() => setSearch('')}>
            <Ionicons name="close-circle" size={18} color="#94a3b8" />
          </TouchableOpacity>
        ) : null}
      </View>

      {/* Tag Filter */}
      {allTags.length > 0 && (
        <FlatList
          horizontal
          data={allTags}
          keyExtractor={t => t.id}
          showsHorizontalScrollIndicator={false}
          style={styles.tagFilter}
          contentContainerStyle={{ paddingHorizontal: 16 }}
          renderItem={({ item }) => (
            <TouchableOpacity
              onPress={() => setActiveTag(activeTag === item.id ? null : item.id)}
              style={[
                styles.filterChip,
                activeTag === item.id && { backgroundColor: item.color },
              ]}
            >
              <Text style={[styles.filterChipText, activeTag === item.id && { color: '#fff' }]}>
                {item.name}
              </Text>
            </TouchableOpacity>
          )}
        />
      )}

      {/* Notes List */}
      <FlatList
        data={filtered}
        keyExtractor={n => n.id}
        renderItem={renderNote}
        contentContainerStyle={{ padding: 16, paddingBottom: 100 }}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={load} />}
        ListEmptyComponent={
          <View style={styles.empty}>
            <Ionicons name="document-text-outline" size={48} color="#cbd5e1" />
            <Text style={styles.emptyText}>No notes yet</Text>
          </View>
        }
      />

      {/* FAB */}
      <TouchableOpacity
        style={styles.fab}
        onPress={() => navigation.navigate('NoteEditor', {})}
      >
        <Ionicons name="add" size={32} color="#fff" />
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f8fafc' },
  header: {
    flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center',
    paddingHorizontal: 20, paddingTop: 60, paddingBottom: 12,
    backgroundColor: '#fff', borderBottomWidth: 1, borderBottomColor: '#f1f5f9',
  },
  headerTitle: { fontSize: 24, fontWeight: '700', color: '#1e293b', letterSpacing: -0.5 },
  searchRow: {
    flexDirection: 'row', alignItems: 'center',
    margin: 16, paddingHorizontal: 14, paddingVertical: 10,
    backgroundColor: '#fff', borderRadius: 12,
    shadowColor: '#000', shadowOpacity: 0.05, shadowRadius: 4, elevation: 2,
  },
  searchInput: { flex: 1, fontSize: 15, color: '#1e293b' },
  tagFilter: { maxHeight: 44, marginBottom: 4 },
  filterChip: {
    paddingHorizontal: 14, paddingVertical: 6, borderRadius: 20,
    backgroundColor: '#f1f5f9', marginRight: 8,
  },
  filterChipText: { fontSize: 13, fontWeight: '500', color: '#475569' },
  card: {
    backgroundColor: '#fff', borderRadius: 16, marginBottom: 12,
    overflow: 'hidden',
    shadowColor: '#000', shadowOpacity: 0.06, shadowRadius: 8, elevation: 2,
  },
  cardPhoto: { width: '100%', height: 160, resizeMode: 'cover' },
  cardBody: { padding: 14 },
  cardTitle: { fontSize: 16, fontWeight: '600', color: '#1e293b', marginBottom: 4 },
  cardPreview: { fontSize: 14, color: '#64748b', lineHeight: 20, marginBottom: 8 },
  tagRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginBottom: 8 },
  tag: { paddingHorizontal: 8, paddingVertical: 3, borderRadius: 6 },
  tagText: { fontSize: 11, fontWeight: '600' },
  cardDate: { fontSize: 11, color: '#94a3b8' },
  empty: { alignItems: 'center', paddingTop: 80 },
  emptyText: { color: '#94a3b8', marginTop: 12, fontSize: 16 },
  fab: {
    position: 'absolute', right: 24, bottom: 32,
    width: 60, height: 60, borderRadius: 30,
    backgroundColor: '#6366f1', alignItems: 'center', justifyContent: 'center',
    shadowColor: '#6366f1', shadowOpacity: 0.4, shadowRadius: 12, elevation: 8,
  },
});
