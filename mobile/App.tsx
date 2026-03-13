import React, { useEffect, useState } from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { View, ActivityIndicator, StyleSheet } from 'react-native';
import { Session } from '@supabase/supabase-js';

import { supabase } from './src/services/supabase';
import AuthScreen from './src/screens/AuthScreen';
import NoteListScreen from './src/screens/NoteListScreen';
import NoteEditorScreen from './src/screens/NoteEditorScreen';
import TagManagerScreen from './src/screens/TagManagerScreen';
import { RootStackParamList } from './src/types';

const Stack = createNativeStackNavigator<RootStackParamList>();

export default function App() {
  const [session, setSession] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Listen for auth state changes first, before checking session
    // This ensures we catch the signOut event from supabase.ts
    const { data: { subscription } } = supabase.auth.onAuthStateChange((_event, session) => {
      setSession(session);
      setLoading(false);
    });

    // Then trigger a session check — this will fire onAuthStateChange
    supabase.auth.getSession().then(({ data: { session } }) => {
      setSession(session);
      setLoading(false);
    });

    return () => subscription.unsubscribe();
  }, []);

  // Show spinner while checking session on launch
  if (loading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#6366f1" />
      </View>
    );
  }

  return (
    <SafeAreaProvider>
      <NavigationContainer>
        <Stack.Navigator id={undefined} screenOptions={{ headerShown: false }}>
          {session ? (
            // Authenticated screens
            <>
              <Stack.Screen name="NoteList" component={NoteListScreen} />
              <Stack.Screen name="NoteEditor" component={NoteEditorScreen} />
              <Stack.Screen name="TagManager" component={TagManagerScreen} />
            </>
          ) : (
            // Unauthenticated
            <Stack.Screen name="Auth" component={AuthScreen} />
          )}
        </Stack.Navigator>
      </NavigationContainer>
    </SafeAreaProvider>
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#f8fafc',
  },
});
