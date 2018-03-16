package com.thucloud.conflictreaper.WebSocket;

import java.util.Collections;
import java.util.HashMap;
import java.util.Map;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;

import com.thucloud.conflictreaper.Service.DataService;

public class ChatWebSocketHandler extends TextWebSocketHandler {
	
	private final static Map<String, WebSocketSession> sessions = Collections.synchronizedMap(new HashMap<String, WebSocketSession>());
	@Autowired
	private DataService dataService;
	
	public DataService getDataService() {
		return dataService;
	}
	
	public void setDataService(DataService dataService) {
		this.dataService = dataService;
	}
	
	public static boolean isOnline(String username) {
		WebSocketSession session = sessions.get(username);
		if (session != null)
			return true;
		else
			return false;
	}
	
    @Override
    protected void handleTextMessage(WebSocketSession session, TextMessage message) throws Exception {
    	String[] params = message.getPayload().split(",");
    	String ip = params[0].substring(3);
    	int port = Integer.parseInt(params[1].substring(5));
    	int status = 1;
    	String name = (String) session.getAttributes().get("user");
    	dataService.register(name, ip, port, status);
        super.handleTextMessage(session, message);
    }
    
    @Override
    public void afterConnectionEstablished(WebSocketSession session) throws Exception {
        sessions.put((String) session.getAttributes().get("user"), session);
    }
    
    @Override
    public void handleTransportError(WebSocketSession session, Throwable exception) throws Exception {
        if(session.isOpen()){
            session.close();
        }
        String name = (String) session.getAttributes().get("user");
        sessions.remove(name);
        dataService.updateStatus(name, 0);
    }
    
    @Override
    public void afterConnectionClosed(WebSocketSession session, CloseStatus closeStatus) throws Exception {
    	String name = (String) session.getAttributes().get("user");
        sessions.remove(name);
        dataService.updateStatus(name, 0);
    }

    @Override
    public boolean supportsPartialMessages() {
        return false;
    }
    
}
