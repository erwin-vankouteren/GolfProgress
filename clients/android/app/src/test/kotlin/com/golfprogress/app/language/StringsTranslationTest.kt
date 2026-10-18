package com.golfprogress.app.language

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.w3c.dom.Element
import java.io.File
import javax.xml.parsers.DocumentBuilderFactory

/**
 * Every string has its Dutch, with the same format arguments, so a new string can't
 * ship in English only or crash on a missing argument in Dutch.
 */
class StringsTranslationTest {
    private val english = read("values")
    private val dutch = read("values-nl")

    @Test
    fun everyStringHasItsDutch_AndNothingElse() {
        assertEquals(english.keys - dutch.keys, emptySet<String>())
        assertEquals(dutch.keys - english.keys, emptySet<String>())
    }

    @Test
    fun dutchUsesTheSameFormatArguments() {
        val mismatched =
            english.filter { (name, texts) ->
                val dutchTexts = dutch[name] ?: return@filter false
                texts.map(::arguments).toSet() != dutchTexts.map(::arguments).toSet()
            }
        assertEquals(emptyMap<String, List<String>>(), mismatched)
    }

    @Test
    fun noDutchStringIsEmpty() {
        assertTrue(dutch.values.flatten().none { it.isBlank() })
    }

    private fun arguments(text: String) =
        Regex("""%(\d+\$)?[,]?[sd]""")
            .findAll(text)
            .map { it.value }
            .sorted()
            .toList()

    /** Strings by name; a plural's name maps to each of its quantities' texts. */
    private fun read(folder: String): Map<String, List<String>> {
        val document = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(File("src/main/res/$folder/strings.xml"))
        val root = document.documentElement
        val entries = mutableMapOf<String, List<String>>()
        val strings = root.getElementsByTagName("string")
        for (i in 0 until strings.length) {
            val element = strings.item(i) as Element
            entries[element.getAttribute("name")] = listOf(element.textContent)
        }
        val plurals = root.getElementsByTagName("plurals")
        for (i in 0 until plurals.length) {
            val element = plurals.item(i) as Element
            val items = element.getElementsByTagName("item")
            entries["plurals/" + element.getAttribute("name")] = (0 until items.length).map { items.item(it).textContent }
        }
        return entries
    }
}
