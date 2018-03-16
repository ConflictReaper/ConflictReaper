package com.thucloud.conflictreaper.Controller;

import java.io.BufferedReader;
import java.io.UnsupportedEncodingException;

import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;
import org.springframework.web.bind.annotation.ResponseBody;

import com.thucloud.conflictreaper.Service.DataService;

@Controller
public class DataController {
	@Autowired
	private DataService dataService;

	public DataService getDataService() {
		return dataService;
	}

	public void setDataService(DataService dataService) {
		this.dataService = dataService;
	}
	
	@RequestMapping(value = "/getaddressmap", method = RequestMethod.POST, produces="text/html;utf-8")
	@ResponseBody
	public String getAddressMap(HttpServletRequest request, HttpServletResponse response) {
		try {
			request.setCharacterEncoding("UTF-8");
		} catch (UnsupportedEncodingException e) {
			e.printStackTrace();
		}
		
		StringBuffer buffer = new StringBuffer();
		String line = null;
		try {
		    BufferedReader reader = request.getReader();
		    while ((line = reader.readLine()) != null)
		        buffer.append(line);
		} catch (Exception e) { /*report an error*/ }
		String msg = buffer.toString();
		
		return dataService.getAddressMap(msg);
	}
}
