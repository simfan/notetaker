import React, { useEffect, useState } from 'react';
import {
  View, Text, FlatList, TouchableOpacity, TextInput,
  StyleSheet, Alert, Modal,
} from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { Ionicons } from '@expo/vector-icons';
import { Tag } from '../types';
import { fetchTags, createTag, deleteTag } from '../services/notesService';

const PALETTE = [
  '#6366f1','#10b981','#f59e0b','#ef4444',
  '#8b5cf6','#06b6d4','#f97316','#ec4899',
  '#14b8a6','#84cc16','#a855f7','#3b82f6',
];

export default function TagManagerScreen() {
  const navigation = useNavigation<any>();
  const [tags, setTags] = useState<Tag[]>([]);
  const [modalVisible, setModalVisible] = useState(false);
  const [newName, setNewName] = useState('');
  const [newColor, setNewColor] = useState(PALETTE[0]);

  const load = async () => {
    const data = await fetchTags();
    setTags(data);
  };

  useEffect(() => { load(); }, []);

  const handleCreate = async () => {
    if (!newName.trim()) return;
    try {
      await createTag(newName.trim(), newColor);
      setNewName('');
      setNewColor(PALETTE[0]);
      setModalVisible(false);
      load();
    } catch (e: any) {
      Alert.alert('Error', e.message);
    }
  };

  const handleDelete = (tag: Tag) => {
    Alert.alert('Delete Tag', `Delete "${tag.name}"? Notes will keep their content.`, [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Delete', style: 'destructive', onPress: async () => {
        await deleteTag(tag.id);
        load();
      }},
    ]);
  };

  return (
    <View style={styles.container}>
      <View style={styles.navbar}>
        <TouchableOpacity onPress={() => navigation.goBack()}>
          <Ionicons name="chevron-back" size={24} color="#6366f1" />
        </TouchableOpacity>
        <Text style={styles.navTitle}>Tag Manager</Text>
        <TouchableOpacity onPress={() => setModalVisible(true)} style={styles.addBtn}>
          <Ionicons name="add" size={22} color="#fff" />
        </TouchableOpacity>
      </View>

      <FlatList
        data={tags}
        keyExtractor={t => t.id}
        contentContainerStyle={{ padding: 16 }}
        renderItem={({ item }) => (
          <View style={styles.tagRow}>
            <View style={[styles.colorDot, { backgroundColor: item.color }]} />
            <Text style={styles.tagName}>{item.name}</Text>
            <TouchableOpacity onPress={() => handleDelete(item)}>
              <Ionicons name="trash-outline" size={20} color="#ef4444" />
            </TouchableOpacity>
          </View>
        )}
        ListEmptyComponent={
          <View style={styles.empty}>
            <Ionicons name="pricetags-outline" size={48} color="#cbd5e1" />
            <Text style={styles.emptyText}>No tags yet</Text>
          </View>
        }
      />

      {/* Create Tag Modal */}
      <Modal visible={modalVisible} transparent animationType="slide">
        <View style={styles.modalOverlay}>
          <View style={styles.modalSheet}>
            <Text style={styles.modalTitle}>New Tag</Text>
            <TextInput
              style={styles.modalInput}
              placeholder="Tag name…"
              placeholderTextColor="#94a3b8"
              value={newName}
              onChangeText={setNewName}
              autoFocus
            />
            <Text style={styles.colorLabel}>Pick a color</Text>
            <View style={styles.palette}>
              {PALETTE.map(c => (
                <TouchableOpacity key={c} onPress={() => setNewColor(c)}>
                  <View style={[styles.paletteColor, { backgroundColor: c },
                    newColor === c && styles.paletteSelected]} />
                </TouchableOpacity>
              ))}
            </View>
            <View style={styles.modalActions}>
              <TouchableOpacity onPress={() => setModalVisible(false)} style={styles.cancelBtn}>
                <Text style={styles.cancelText}>Cancel</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={handleCreate} style={styles.createBtn}>
                <Text style={styles.createText}>Create</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#f8fafc' },
  navbar: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
    paddingTop: 60, paddingBottom: 12, paddingHorizontal: 16,
    backgroundColor: '#fff', borderBottomWidth: 1, borderBottomColor: '#f1f5f9',
  },
  navTitle: { fontSize: 17, fontWeight: '600', color: '#1e293b' },
  addBtn: {
    backgroundColor: '#6366f1', width: 32, height: 32,
    borderRadius: 10, alignItems: 'center', justifyContent: 'center',
  },
  tagRow: {
    flexDirection: 'row', alignItems: 'center',
    backgroundColor: '#fff', borderRadius: 12, padding: 14,
    marginBottom: 10, shadowColor: '#000', shadowOpacity: 0.04, shadowRadius: 4, elevation: 1,
  },
  colorDot: { width: 14, height: 14, borderRadius: 7, marginRight: 12 },
  tagName: { flex: 1, fontSize: 15, fontWeight: '500', color: '#1e293b' },
  empty: { alignItems: 'center', paddingTop: 80 },
  emptyText: { color: '#94a3b8', marginTop: 12, fontSize: 16 },
  modalOverlay: { flex: 1, justifyContent: 'flex-end', backgroundColor: 'rgba(0,0,0,0.3)' },
  modalSheet: {
    backgroundColor: '#fff', borderTopLeftRadius: 24, borderTopRightRadius: 24,
    padding: 24, paddingBottom: 40,
  },
  modalTitle: { fontSize: 18, fontWeight: '700', color: '#1e293b', marginBottom: 16 },
  modalInput: {
    borderWidth: 1.5, borderColor: '#e2e8f0', borderRadius: 12,
    paddingHorizontal: 14, paddingVertical: 12, fontSize: 16, color: '#1e293b', marginBottom: 16,
  },
  colorLabel: { fontSize: 12, fontWeight: '700', color: '#94a3b8', textTransform: 'uppercase', letterSpacing: 1, marginBottom: 12 },
  palette: { flexDirection: 'row', flexWrap: 'wrap', gap: 12, marginBottom: 24 },
  paletteColor: { width: 36, height: 36, borderRadius: 18 },
  paletteSelected: { borderWidth: 3, borderColor: '#1e293b', transform: [{ scale: 1.15 }] },
  modalActions: { flexDirection: 'row', gap: 12 },
  cancelBtn: {
    flex: 1, paddingVertical: 14, borderRadius: 12,
    backgroundColor: '#f1f5f9', alignItems: 'center',
  },
  cancelText: { fontSize: 15, fontWeight: '600', color: '#64748b' },
  createBtn: {
    flex: 1, paddingVertical: 14, borderRadius: 12,
    backgroundColor: '#6366f1', alignItems: 'center',
  },
  createText: { fontSize: 15, fontWeight: '600', color: '#fff' },
});
